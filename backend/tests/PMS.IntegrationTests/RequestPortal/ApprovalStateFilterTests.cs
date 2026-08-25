using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Models;
using PMS.Application.Features.Approvals;
using PMS.Application.Features.SavedViews;
using PMS.Application.Features.WorkItemTypes;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.RequestPortal;

/// <summary>
/// <c>TaskField.ApprovalState</c> (ADR-063) — trả nốt lời hứa còn treo của ADR-061:
/// <i>"hàng đợi của một quy trình chính là một view lưu được"</i>.
///
/// <para>
/// 🔑 Đây là trường dựng sẵn ĐẦU TIÊN không phải một cột của bảng <c>Tasks</c> — nó là phép
/// chiếu của bảng <c>Approvals</c> xuống task. Rủi ro vì vậy nằm ở chỗ khác mọi bộ lọc
/// trước nó: <b>ba nơi phải nhìn cùng một tập hàng</b> (<c>ConsumedAt IS NULL</c>) —
/// guard <c>EnsureApprovedAsync</c>, nhánh lọc ở <c>TaskRepository</c>, và phép chiếu ở
/// <c>RequestPortalService</c>. Lệch một chỗ là màn hình nói khác thứ cổng cưỡng chế.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ApprovalStateFilterTests : IntegrationTestBase
{
    public ApprovalStateFilterTests(PmsWebApplicationFactory factory) : base(factory) { }

    private static async Task<List<Guid>> QueryAsync(
        HttpClient client, Guid projectId, TaskApprovalState state,
        FilterOperator op = FilterOperator.Equals)
    {
        var res = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/tasks/query",
            new TaskQueryRequest(Filters:
            [
                new SavedViewFilterDto(TaskField.ApprovalState, null, op, state.ToString())
            ]));

        res.StatusCode.ShouldBe(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        var page = (await res.Content.ReadFromJsonAsync<PagedResult<TaskListItemResponse>>(
            TestJson.Options))!;
        return page.Items.Select(i => i.Task.Id).ToList();
    }

    /// <summary>
    /// Yêu cầu duyệt đang CÒN HIỆU LỰC của một task.
    /// <para>
    /// ⚠️ <c>GET /tasks/{id}/approvals</c> trả <see cref="TaskApprovalsResponse"/>
    /// (<c>hasGate</c> + <c>active</c> + <c>history</c>), <b>không</b> phải một mảng trần —
    /// <c>hasGate</c> là thứ nuôi việc TỰ ẨN khối duyệt ở màn chi tiết (luật 3 Doctrine).
    /// </para>
    /// </summary>
    private static async Task<ApprovalResponse> ActiveApprovalAsync(HttpClient client, Guid taskId)
    {
        var res = (await client.GetFromJsonAsync<TaskApprovalsResponse>(
            $"/api/v1/tasks/{taskId}/approvals", TestJson.Options))!;

        res.HasGate.ShouldBeTrue("Task này phải nằm dưới một cổng duyệt.");
        return res.Active.ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> ApproveAsync(HttpClient client, Guid approvalId)
        => client.PostAsJsonAsync($"/api/v1/approvals/{approvalId}/decisions",
            new CreateApprovalDecisionRequest(DecisionKind.Approve, "OK"));

    /// <summary>Project có một cổng duyệt trên cột thứ hai, và một task đã chạm cổng đó.</summary>
    private async Task<(TestUser Pm, Guid ProjectId, Guid GatedTask, Guid CleanTask)> SeedGateAsync()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client, $"CAB {Guid.NewGuid():N}");

        var types = (await pm.Client.GetFromJsonAsync<List<WorkItemTypeResponse>>(
            $"/api/v1/projects/{projectId}/work-item-types", TestJson.Options))!;
        var gate = await ColumnIdAsync(projectId, order: 1);

        await pm.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/approval-policies",
            new CreateApprovalPolicyRequest(types[0].Id, gate, ApproverMode.ProjectManagers, 1, null));

        var gated = await CreateTaskAsync(pm.Client, projectId, "Cần duyệt");
        var clean = await CreateTaskAsync(pm.Client, projectId, "Không chạm cổng");

        // Kéo vào cột có cổng → 409 + TỰ SINH yêu cầu duyệt (ADR-062, không có nút "Gửi duyệt").
        var moved = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{gated}/status",
            new { targetColumnId = gate });
        moved.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        return (pm, projectId, gated, clean);
    }

    [Fact]
    public async Task Loc_Pending_tra_dung_task_dang_cho_ky()
    {
        var (pm, projectId, gated, clean) = await SeedGateAsync();

        var pending = await QueryAsync(pm.Client, projectId, TaskApprovalState.Pending);
        pending.ShouldContain(gated);
        pending.ShouldNotContain(clean);

        var none = await QueryAsync(pm.Client, projectId, TaskApprovalState.None);
        none.ShouldContain(clean);
        none.ShouldNotContain(gated);
    }

    [Fact]
    public async Task Loc_Approved_tra_task_da_du_quorum_ma_CHUA_ai_keo_qua()
    {
        var (pm, projectId, gated, _) = await SeedGateAsync();

        var live = await ActiveApprovalAsync(pm.Client, gated);

        // ⚠️ 200, KHÔNG phải 201 — endpoint trả về chính aggregate Approval đã cập nhật
        // (approveCount/status/canDecide), chứ không phải một tài nguyên Decision mới ở một
        // URL riêng. Ghi ra đây vì 201 là phỏng đoán tự nhiên cho một POST và nó SAI.
        var decided = await ApproveAsync(pm.Client, live.Id);
        decided.StatusCode.ShouldBe(HttpStatusCode.OK, await decided.Content.ReadAsStringAsync());

        // Đã duyệt nhưng CHƯA tiêu thụ — một trạng thái ngắn nhưng có thật, và chính là
        // hàng đợi "đã được duyệt, đang chờ ai đó thực thi".
        (await QueryAsync(pm.Client, projectId, TaskApprovalState.Approved)).ShouldContain(gated);
        (await QueryAsync(pm.Client, projectId, TaskApprovalState.Pending)).ShouldNotContain(gated);
    }

    [Fact]
    public async Task Loc_Rejected_tra_task_bi_tu_choi_va_VAN_dang_chan()
    {
        var (pm, projectId, gated, _) = await SeedGateAsync();

        var live = await ActiveApprovalAsync(pm.Client, gated);

        var rejected = await pm.Client.PostAsJsonAsync($"/api/v1/approvals/{live.Id}/decisions",
            new CreateApprovalDecisionRequest(DecisionKind.Reject, "Rủi ro quá cao"));
        rejected.StatusCode.ShouldBe(HttpStatusCode.OK, await rejected.Content.ReadAsStringAsync());

        // 🔴 Lời từ chối VẪN CHẶN tới khi có người huỷ tường minh (ADR-062 quyết định c),
        // nên nó vẫn là một hàng đợi CẦN HÀNH ĐỘNG — không phải một kết quả đã đóng sổ.
        // Nếu nó rơi về None ở đây thì bộ lọc đang nói ngược với thứ cổng cưỡng chế.
        (await QueryAsync(pm.Client, projectId, TaskApprovalState.Rejected)).ShouldContain(gated);
        (await QueryAsync(pm.Client, projectId, TaskApprovalState.None)).ShouldNotContain(gated);
    }

    [Fact]
    public async Task Task_da_di_qua_cong_quay_ve_None()
    {
        var (pm, projectId, gated, _) = await SeedGateAsync();
        var gate = await ColumnIdAsync(projectId, order: 1);

        var live = await ActiveApprovalAsync(pm.Client, gated);

        (await ApproveAsync(pm.Client, live.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var moved = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{gated}/status",
            new { targetColumnId = gate });
        moved.StatusCode.ShouldBe(HttpStatusCode.OK, await moved.Content.ReadAsStringAsync());

        // Hàng cũ nay ConsumedAt != null. Bảng Approvals là NHẬT KÝ — hàng đó ở nguyên đó
        // vĩnh viễn để trả lời "ai đã ký". Nếu bộ lọc quên `ConsumedAt IS NULL` thì task này
        // sẽ kẹt trong hàng đợi "đã duyệt" mãi mãi.
        (await QueryAsync(pm.Client, projectId, TaskApprovalState.None)).ShouldContain(gated);
        (await QueryAsync(pm.Client, projectId, TaskApprovalState.Approved)).ShouldNotContain(gated);
    }

    [Fact]
    public async Task NotEquals_la_phu_dinh_ca_menh_de_chu_khong_phai_trang_thai_khac()
    {
        var (pm, projectId, gated, clean) = await SeedGateAsync();

        // ⚠️ "khác Pending" phải BAO GỒM task chưa từng chạm cổng. Đọc theo nghĩa "có một
        // hàng còn hiệu lực mang trạng thái khác Pending" sẽ giấu mất phần lớn hệ thống —
        // và người dùng không có cách nào biết là họ đang bị giấu.
        var notPending = await QueryAsync(
            pm.Client, projectId, TaskApprovalState.Pending, FilterOperator.NotEquals);

        notPending.ShouldContain(clean);
        notPending.ShouldNotContain(gated);
    }

    [Fact]
    public async Task Hang_doi_duyet_luu_duoc_thanh_mot_SavedView()
    {
        // 🔑 Đây là phép nghiệm thu lời hứa của ADR-061, chứ không phải một test lọc nữa:
        // hàng đợi của một quy trình phải LƯU được, không chỉ gõ được một lần.
        var (pm, projectId, gated, clean) = await SeedGateAsync();

        var created = await pm.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/views",
            new CreateSavedViewRequest(
                Name: "CR đang chờ duyệt",
                IsShared: true,
                SortBy: null,
                SortDescending: false,
                GroupBy: null,
                Filters: [new SavedViewFilterDto(
                    TaskField.ApprovalState, null, FilterOperator.Equals,
                    TaskApprovalState.Pending.ToString())],
                Columns: null));

        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var view = (await created.Content.ReadFromJsonAsync<SavedViewResponse>(TestJson.Options))!;
        view.Filters.Single().Field.ShouldBe(TaskField.ApprovalState);

        // Và view đọc lại được đúng như đã lưu — bộ lọc là BẢNG QUAN HỆ, không phải JSON blob.
        var reloaded = await WithDbAsync(db => db.SavedViewFilters
            .AsNoTracking()
            .SingleAsync(f => f.SavedViewId == view.Id));
        reloaded.Field.ShouldBe(TaskField.ApprovalState);

        var result = await QueryAsync(pm.Client, projectId, TaskApprovalState.Pending);
        result.ShouldContain(gated);
        result.ShouldNotContain(clean);
    }
}
