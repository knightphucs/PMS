using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Features.Approvals;
using PMS.Application.Features.BoardColumns;
using PMS.Application.Features.Tasks;
using PMS.Application.Features.WorkItemTypes;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Approvals;

/// <summary>
/// Phê duyệt là dữ liệu (ADR-062) — ĐỘNG TỪ đầu tiên của hệ thống.
///
/// <para>
/// Rủi ro của tính năng này nằm ở bốn chỗ khác hẳn CRUD thường:
/// <list type="number">
/// <item><b>Guard phải THẬT SỰ chặn.</b> Có một test dành riêng cho phép kiểm này
/// (<see cref="Guard_duyet_la_diem_cuong_che_that"/>) — gỡ <c>EnsureApprovedAsync</c> ra thì
/// nó phải đỏ. Một guard không có test nào chết khi gỡ nó ra là một guard chưa được chứng
/// minh là đang chạy (tiền lệ: @mention ADR-048, <c>IsRequired</c> ADR-060).</item>
/// <item><b>Quyền KÝ không đi qua <c>RoleInProject</c></b> — ngoại lệ có chủ đích của mô
/// hình hai tầng. Không có test thì phiên sau sẽ "sửa" nó về cho nhất quán.</item>
/// <item><b>Vòng đời "duyệt lại"</b> — <c>ConsumedAt</c> là thứ khiến lần quay lại sinh một
/// yêu cầu MỚI thay vì đi nhờ chữ ký cũ.</item>
/// <item><b>Cascade Restrict</b> — xoá loại việc / cột đích đang có luật duyệt không được
/// 500.</item>
/// </list>
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ApprovalsTests : IntegrationTestBase
{
    public ApprovalsTests(PmsWebApplicationFactory factory) : base(factory) { }

    // ---------- helper ----------

    private static async Task<Guid> DefaultTypeIdAsync(HttpClient client, Guid projectId)
    {
        var types = await client.GetFromJsonAsync<List<WorkItemTypeResponse>>(
            $"/api/v1/projects/{projectId}/work-item-types", TestJson.Options);
        return types!.First().Id;
    }

    private static async Task<HttpResponseMessage> PostPolicyAsync(
        HttpClient client, Guid projectId, Guid typeId, Guid columnId,
        ApproverMode mode = ApproverMode.ProjectManagers, int minApprovals = 1,
        IReadOnlyList<Guid>? approverIds = null)
        => await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/approval-policies",
            new CreateApprovalPolicyRequest(typeId, columnId, mode, minApprovals, approverIds));

    private static async Task<ApprovalPolicyResponse> CreatePolicyAsync(
        HttpClient client, Guid projectId, Guid typeId, Guid columnId,
        ApproverMode mode = ApproverMode.ProjectManagers, int minApprovals = 1,
        IReadOnlyList<Guid>? approverIds = null)
    {
        var res = await PostPolicyAsync(client, projectId, typeId, columnId, mode, minApprovals, approverIds);
        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<ApprovalPolicyResponse>(TestJson.Options))!;
    }

    /// <summary>Đổi cột KHÔNG khẳng định 200 — ở đây 409 là kết quả hợp lệ và hay gặp.</summary>
    private static async Task<HttpResponseMessage> TryMoveAsync(
        HttpClient client, Guid taskId, Guid columnId)
        => await client.PatchAsJsonAsync(
            $"/api/v1/tasks/{taskId}/status", new ChangeTaskStatusRequest(columnId));

    private static async Task<TaskApprovalsResponse> GetApprovalsAsync(
        HttpClient client, Guid taskId)
        => (await client.GetFromJsonAsync<TaskApprovalsResponse>(
            $"/api/v1/tasks/{taskId}/approvals", TestJson.Options))!;

    private static async Task<HttpResponseMessage> DecideAsync(
        HttpClient client, Guid approvalId, DecisionKind kind, string? comment = null)
        => await client.PostAsJsonAsync($"/api/v1/approvals/{approvalId}/decisions",
            new CreateApprovalDecisionRequest(kind, comment));

    private Task<Guid> ColumnOfTask(Guid taskId, int order) => ColumnOfTaskAsync(taskId, order);

    /// <summary>PM + project + task + cột đích (order 1), tức bộ khung của gần như mọi test.</summary>
    private async Task<(TestUser Pm, Guid ProjectId, Guid TaskId, Guid TypeId, Guid GateColumnId)>
        SetupAsync()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var typeId = await DefaultTypeIdAsync(pm.Client, projectId);
        var gate = await ColumnOfTask(taskId, 1);

        return (pm, projectId, taskId, typeId, gate);
    }

    // ---------- luật duyệt: CRUD + quyền ----------

    [Fact]
    public async Task PM_tao_duoc_luat_duyet_va_doc_lai_duoc()
    {
        var (pm, projectId, _, typeId, gate) = await SetupAsync();

        var policy = await CreatePolicyAsync(pm.Client, projectId, typeId, gate,
            ApproverMode.ProjectManagers, 2);

        policy.MinApprovals.ShouldBe(2);
        policy.TargetColumnId.ShouldBe(gate);
        // Navigation phải được đặt lúc tạo, nếu không ToResponse ném NRE (bẫy ADR-060).
        policy.WorkItemTypeName.ShouldNotBeNullOrWhiteSpace();
        policy.TargetColumnName.ShouldNotBeNullOrWhiteSpace();

        var list = await pm.Client.GetFromJsonAsync<List<ApprovalPolicyResponse>>(
            $"/api/v1/projects/{projectId}/approval-policies", TestJson.Options);
        list!.ShouldHaveSingleItem().Id.ShouldBe(policy.Id);
    }

    [Fact]
    public async Task Member_KHONG_tao_duoc_luat_duyet_nhung_VAN_doc_duoc()
    {
        var (pm, projectId, _, typeId, gate) = await SetupAsync();
        var member = await CreateUserAsync();
        await AddMemberAsync(pm.Client, member, projectId, RoleInProject.Member);

        await CreatePolicyAsync(pm.Client, projectId, typeId, gate);

        var created = await PostPolicyAsync(member.Client, projectId, typeId, gate);
        created.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Đọc thì được: giấu luật đi chỉ khiến một thẻ không kéo được trở thành bí ẩn.
        var list = await member.Client.GetFromJsonAsync<List<ApprovalPolicyResponse>>(
            $"/api/v1/projects/{projectId}/approval-policies", TestJson.Options);
        list!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404_chu_khong_403()
    {
        var (pm, projectId, _, typeId, gate) = await SetupAsync();
        await CreatePolicyAsync(pm.Client, projectId, typeId, gate);

        var outsider = await CreateUserAsync();

        // 404 chứ không 403 — 403 sẽ tiết lộ project đó tồn tại (ADR-006).
        var res = await outsider.Client.GetAsync($"/api/v1/projects/{projectId}/approval-policies");
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Hai_luat_cho_cung_mot_cong_bi_tu_choi_409()
    {
        var (pm, projectId, _, typeId, gate) = await SetupAsync();
        await CreatePolicyAsync(pm.Client, projectId, typeId, gate);

        var second = await PostPolicyAsync(pm.Client, projectId, typeId, gate);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Loai_viec_cua_project_khac_bi_tu_choi_404()
    {
        var (pm, projectId, _, _, gate) = await SetupAsync();

        var otherProjectId = await CreateProjectAsync(pm.Client, "KHAC");
        var foreignTypeId = await DefaultTypeIdAsync(pm.Client, otherProjectId);

        // Ranh giới chéo project: id có thật, nhưng không thuộc project này -> 404.
        var res = await PostPolicyAsync(pm.Client, projectId, foreignTypeId, gate);
        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Quorum_lon_hon_so_nguoi_duyet_bi_tu_choi_400()
    {
        var (pm, projectId, _, typeId, gate) = await SetupAsync();

        // Cổng sẽ KHÔNG BAO GIỜ mở được -> không được phép lưu (luật 4 của Doctrine §0).
        var res = await PostPolicyAsync(pm.Client, projectId, typeId, gate,
            ApproverMode.NamedApprovers, minApprovals: 2, approverIds: [pm.EmployeeId]);

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Nguoi_duyet_khong_phai_thanh_vien_bi_tu_choi_400()
    {
        var (pm, projectId, _, typeId, gate) = await SetupAsync();
        var outsider = await CreateUserAsync();

        var res = await PostPolicyAsync(pm.Client, projectId, typeId, gate,
            ApproverMode.NamedApprovers, 1, [outsider.EmployeeId]);

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---------- guard: bốn nhánh ----------

    [Fact]
    public async Task Khong_co_cong_thi_doi_cot_chay_y_nhu_truoc()
    {
        var (pm, _, taskId, _, gate) = await SetupAsync();

        var res = await TryMoveAsync(pm.Client, taskId, gate);
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var approvals = await GetApprovalsAsync(pm.Client, taskId);
        approvals.HasGate.ShouldBeFalse();
        approvals.Active.ShouldBeNull();
        approvals.History.ShouldBeEmpty();
    }

    /// <summary>
    /// 🔴 MUTATION TEST của cả hạng mục. Gỡ lời gọi <c>EnsureApprovedAsync</c> trong
    /// <c>TaskStatusTransitionService</c> thì test này PHẢI đỏ ở dòng
    /// <c>ShouldBe(HttpStatusCode.Conflict)</c>.
    /// </summary>
    [Fact]
    public async Task Guard_duyet_la_diem_cuong_che_that()
    {
        var (pm, projectId, taskId, typeId, gate) = await SetupAsync();
        await CreatePolicyAsync(pm.Client, projectId, typeId, gate);

        var res = await TryMoveAsync(pm.Client, taskId, gate);

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Và task PHẢI còn nguyên ở cột cũ — 409 mà vẫn di chuyển là guard nói dối.
        var stillAtStart = await WithDbAsync(db => db.Tasks
            .AsNoTracking()
            .Where(t => t.Id == taskId)
            .Select(t => t.BoardColumn.Order)
            .FirstAsync());
        stillAtStart.ShouldBe(0);
    }

    [Fact]
    public async Task Lan_keo_dau_tien_SINH_yeu_cau_duyet_va_bao_cho_nguoi_duyet()
    {
        var (pm, projectId, taskId, typeId, gate) = await SetupAsync();
        var approver = await CreateUserAsync();
        await AddMemberAsync(pm.Client, approver, projectId, RoleInProject.Member);

        await CreatePolicyAsync(pm.Client, projectId, typeId, gate,
            ApproverMode.NamedApprovers, 1, [approver.EmployeeId]);

        var before = await CountNotificationsAsync(approver.EmployeeId);

        var res = await TryMoveAsync(pm.Client, taskId, gate);
        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // 🔴 Tác dụng phụ phải SỐNG SÓT qua ngoại lệ — lưu trước khi ném.
        var approvals = await GetApprovalsAsync(pm.Client, taskId);
        approvals.HasGate.ShouldBeTrue();
        approvals.Active.ShouldNotBeNull();
        approvals.Active!.Status.ShouldBe(ApprovalStatus.Pending);
        approvals.Active.RequestedById.ShouldBe(pm.EmployeeId);
        approvals.Active.RequestedByName.ShouldNotBeNullOrWhiteSpace();

        (await CountNotificationsAsync(approver.EmployeeId)).ShouldBe(before + 1);
    }

    [Fact]
    public async Task Keo_lan_thu_hai_KHONG_sinh_them_yeu_cau()
    {
        var (pm, projectId, taskId, typeId, gate) = await SetupAsync();
        await CreatePolicyAsync(pm.Client, projectId, typeId, gate);

        (await TryMoveAsync(pm.Client, taskId, gate)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await TryMoveAsync(pm.Client, taskId, gate)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Sinh thêm mỗi lần kéo là biến hộp thư người duyệt thành bãi rác.
        var approvals = await GetApprovalsAsync(pm.Client, taskId);
        approvals.History.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Keo_ve_dung_cot_dang_dung_KHONG_sinh_yeu_cau()
    {
        var (pm, projectId, taskId, typeId, _) = await SetupAsync();
        var currentColumn = await ColumnOfTask(taskId, 0);

        await CreatePolicyAsync(pm.Client, projectId, typeId, currentColumn);

        // Chốt no-op đứng TRƯỚC guard, nên thao tác thừa này không được sinh ra thủ tục nào.
        (await TryMoveAsync(pm.Client, taskId, currentColumn)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await GetApprovalsAsync(pm.Client, taskId)).History.ShouldBeEmpty();
    }

    // ---------- quyết định + quorum ----------

    [Fact]
    public async Task Du_quorum_thi_di_qua_duoc_va_moi_lan_ghi_deu_duoc_khang_dinh()
    {
        var (pm, projectId, taskId, typeId, gate) = await SetupAsync();
        var a1 = await CreateUserAsync();
        var a2 = await CreateUserAsync();
        await AddMemberAsync(pm.Client, a1, projectId, RoleInProject.Member);
        await AddMemberAsync(pm.Client, a2, projectId, RoleInProject.Member);

        await CreatePolicyAsync(pm.Client, projectId, typeId, gate,
            ApproverMode.NamedApprovers, 2, [a1.EmployeeId, a2.EmployeeId]);

        (await TryMoveAsync(pm.Client, taskId, gate)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var approvalId = (await GetApprovalsAsync(pm.Client, taskId)).Active!.Id;

        // ⚠️ Khẳng định mã trạng thái ở TỪNG lần ghi, không chỉ lần cuối (bẫy ADR-059).
        (await DecideAsync(a1.Client, approvalId, DecisionKind.Approve)).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        // Một phiếu là chưa đủ.
        (await TryMoveAsync(pm.Client, taskId, gate)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await DecideAsync(a2.Client, approvalId, DecisionKind.Approve)).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        (await TryMoveAsync(pm.Client, taskId, gate)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Mot_phieu_chong_giet_ca_yeu_cau_khong_can_du_quorum()
    {
        var (pm, projectId, taskId, typeId, gate) = await SetupAsync();
        var a1 = await CreateUserAsync();
        var a2 = await CreateUserAsync();
        await AddMemberAsync(pm.Client, a1, projectId, RoleInProject.Member);
        await AddMemberAsync(pm.Client, a2, projectId, RoleInProject.Member);

        await CreatePolicyAsync(pm.Client, projectId, typeId, gate,
            ApproverMode.NamedApprovers, 2, [a1.EmployeeId, a2.EmployeeId]);

        await TryMoveAsync(pm.Client, taskId, gate);
        var approvalId = (await GetApprovalsAsync(pm.Client, taskId)).Active!.Id;

        var rejected = await DecideAsync(a1.Client, approvalId, DecisionKind.Reject,
            "Chưa có kế hoạch rollback");
        rejected.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Phản hồi của chính lời gọi ghi phải ĐẦY ĐỦ, không chỉ lời gọi đọc sau đó — thiếu
        // một Include ở đường ghi là hỏng im lặng mà không test nào bắt được.
        var body = (await rejected.Content.ReadFromJsonAsync<ApprovalResponse>(TestJson.Options))!;
        body.TargetColumnName.ShouldNotBeNullOrWhiteSpace();
        body.Decisions.ShouldHaveSingleItem().ApproverName.ShouldNotBeNullOrWhiteSpace();

        var after = await GetApprovalsAsync(pm.Client, taskId);
        after.Active!.Status.ShouldBe(ApprovalStatus.Rejected);

        // Người thứ hai không còn gì để bỏ phiếu.
        (await DecideAsync(a2.Client, approvalId, DecisionKind.Approve)).StatusCode
            .ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Mot_nguoi_quyet_dinh_hai_lan_bi_tu_choi_409()
    {
        var (pm, projectId, taskId, typeId, gate) = await SetupAsync();
        var a1 = await CreateUserAsync();
        var a2 = await CreateUserAsync();
        await AddMemberAsync(pm.Client, a1, projectId, RoleInProject.Member);
        await AddMemberAsync(pm.Client, a2, projectId, RoleInProject.Member);

        await CreatePolicyAsync(pm.Client, projectId, typeId, gate,
            ApproverMode.NamedApprovers, 2, [a1.EmployeeId, a2.EmployeeId]);

        await TryMoveAsync(pm.Client, taskId, gate);
        var approvalId = (await GetApprovalsAsync(pm.Client, taskId)).Active!.Id;

        (await DecideAsync(a1.Client, approvalId, DecisionKind.Approve)).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        // Quorum 2 nên yêu cầu vẫn Pending — lần thứ hai bị chặn vì TRÙNG NGƯỜI, không phải
        // vì yêu cầu đã chốt.
        (await DecideAsync(a1.Client, approvalId, DecisionKind.Approve)).StatusCode
            .ShouldBe(HttpStatusCode.Conflict);
    }

    // ---------- ngoại lệ có chủ đích: quyền KÝ không theo RoleInProject ----------

    [Fact]
    public async Task NamedApprovers_thi_PM_ngoai_danh_sach_KHONG_ky_duoc()
    {
        var (pm, projectId, taskId, typeId, gate) = await SetupAsync();
        var approver = await CreateUserAsync();
        var otherPm = await CreateUserAsync();
        await AddMemberAsync(pm.Client, approver, projectId, RoleInProject.Member);
        await AddMemberAsync(pm.Client, otherPm, projectId, RoleInProject.ProjectManager);

        await CreatePolicyAsync(pm.Client, projectId, typeId, gate,
            ApproverMode.NamedApprovers, 1, [approver.EmployeeId]);

        await TryMoveAsync(pm.Client, taskId, gate);
        var approvalId = (await GetApprovalsAsync(pm.Client, taskId)).Active!.Id;

        // 🔴 Đây là ngoại lệ có chủ đích của mô hình hai tầng: quyền KÝ theo ApproverMode,
        // KHÔNG theo RoleInProject. Một PM ngoài danh sách vẫn nhận 403 — nếu không thì
        // chế độ "chỉ trưởng phòng ký được" mất hết nghĩa.
        (await DecideAsync(otherPm.Client, approvalId, DecisionKind.Approve)).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        (await DecideAsync(approver.Client, approvalId, DecisionKind.Approve)).StatusCode
            .ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProjectManagers_thi_moi_PM_ky_duoc_du_khong_co_ten_trong_bang()
    {
        var (pm, projectId, taskId, typeId, gate) = await SetupAsync();
        var member = await CreateUserAsync();
        await AddMemberAsync(pm.Client, member, projectId, RoleInProject.Member);

        await CreatePolicyAsync(pm.Client, projectId, typeId, gate, ApproverMode.ProjectManagers);

        await TryMoveAsync(pm.Client, taskId, gate);
        var approvalId = (await GetApprovalsAsync(pm.Client, taskId)).Active!.Id;

        (await DecideAsync(member.Client, approvalId, DecisionKind.Approve)).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        (await DecideAsync(pm.Client, approvalId, DecisionKind.Approve)).StatusCode
            .ShouldBe(HttpStatusCode.OK);
    }

    // ---------- vòng đời "duyệt lại" ----------

    [Fact]
    public async Task Roi_cot_dich_roi_quay_lai_thi_phai_duyet_LAI()
    {
        var (pm, projectId, taskId, typeId, gate) = await SetupAsync();
        await CreatePolicyAsync(pm.Client, projectId, typeId, gate);

        await TryMoveAsync(pm.Client, taskId, gate);
        var first = (await GetApprovalsAsync(pm.Client, taskId)).Active!.Id;
        await DecideAsync(pm.Client, first, DecisionKind.Approve);

        (await TryMoveAsync(pm.Client, taskId, gate)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Chữ ký đã TIÊU THỤ nhưng vẫn ở nguyên Approved — đó là lịch sử kiểm toán.
        var consumed = (await GetApprovalsAsync(pm.Client, taskId)).History.Single();
        consumed.Status.ShouldBe(ApprovalStatus.Approved);
        consumed.ConsumedAt.ShouldNotBeNull();

        // Kéo ra rồi kéo lại.
        var start = await ColumnOfTask(taskId, 0);
        (await TryMoveAsync(pm.Client, taskId, start)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await TryMoveAsync(pm.Client, taskId, gate)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var now = await GetApprovalsAsync(pm.Client, taskId);
        now.History.Count.ShouldBe(2);
        now.Active.ShouldNotBeNull();
        now.Active!.Id.ShouldNotBe(first);
        now.Active.Status.ShouldBe(ApprovalStatus.Pending);
    }

    [Fact]
    public async Task Bi_tu_choi_thi_van_chan_cho_toi_khi_HUY_tuong_minh()
    {
        var (pm, projectId, taskId, typeId, gate) = await SetupAsync();
        await CreatePolicyAsync(pm.Client, projectId, typeId, gate);

        await TryMoveAsync(pm.Client, taskId, gate);
        var approvalId = (await GetApprovalsAsync(pm.Client, taskId)).Active!.Id;
        await DecideAsync(pm.Client, approvalId, DecisionKind.Reject, "Rủi ro quá cao");

        // 🔴 Lời từ chối phải CÓ RĂNG: kéo lại không được âm thầm sinh yêu cầu mới.
        (await TryMoveAsync(pm.Client, taskId, gate)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await GetApprovalsAsync(pm.Client, taskId)).History.Count.ShouldBe(1);

        var cancelled = await pm.Client.PostAsync($"/api/v1/approvals/{approvalId}/cancel", null);
        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Huỷ xong thì cổng mở lại cho một yêu cầu mới.
        (await TryMoveAsync(pm.Client, taskId, gate)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var after = await GetApprovalsAsync(pm.Client, taskId);
        after.History.Count.ShouldBe(2);
        after.Active!.Status.ShouldBe(ApprovalStatus.Pending);
    }

    [Fact]
    public async Task Nguoi_khac_KHONG_huy_duoc_yeu_cau_cua_nguoi_ta()
    {
        var (pm, projectId, taskId, typeId, gate) = await SetupAsync();
        var member = await CreateUserAsync();
        await AddMemberAsync(pm.Client, member, projectId, RoleInProject.Member);
        await CreatePolicyAsync(pm.Client, projectId, typeId, gate);

        await TryMoveAsync(pm.Client, taskId, gate);
        var approvalId = (await GetApprovalsAsync(pm.Client, taskId)).Active!.Id;

        var res = await member.Client.PostAsync($"/api/v1/approvals/{approvalId}/cancel", null);
        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---------- cascade Restrict: xoá loại việc / cột đích ----------

    [Fact]
    public async Task Xoa_cot_dich_dang_co_luat_duyet_KHONG_500()
    {
        var (pm, projectId, taskId, typeId, gate) = await SetupAsync();
        await CreatePolicyAsync(pm.Client, projectId, typeId, gate);
        await TryMoveAsync(pm.Client, taskId, gate);   // sinh cả một hàng Approvals

        // ApprovalPolicies treo dưới BoardColumns bằng Restrict — không dọn trước là 500.
        // Cột đích chưa có task nào (task bị guard chặn lại ở cột đầu) nên không cần cột đích.
        var res = await pm.Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, $"/api/v1/columns/{gate}")
        {
            Content = JsonContent.Create(new DeleteBoardColumnRequest(null)),
        });
        res.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var left = await pm.Client.GetFromJsonAsync<List<ApprovalPolicyResponse>>(
            $"/api/v1/projects/{projectId}/approval-policies", TestJson.Options);
        left!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Xoa_loai_viec_dang_co_luat_duyet_KHONG_500()
    {
        var (pm, projectId, taskId, typeId, gate) = await SetupAsync();

        var extra = await pm.Client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/work-item-types",
            new CreateWorkItemTypeRequest("Change Request", "CircleAlert", "#EF4444"));
        extra.StatusCode.ShouldBe(HttpStatusCode.Created);
        var extraType = (await extra.Content.ReadFromJsonAsync<WorkItemTypeResponse>(TestJson.Options))!;

        await CreatePolicyAsync(pm.Client, projectId, extraType.Id, gate);

        // Loại này chưa có task nào nên không cần loại đích.
        var res = await pm.Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, $"/api/v1/work-item-types/{extraType.Id}")
        {
            Content = JsonContent.Create(new DeleteWorkItemTypeRequest(null)),
        });
        res.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var left = await pm.Client.GetFromJsonAsync<List<ApprovalPolicyResponse>>(
            $"/api/v1/projects/{projectId}/approval-policies", TestJson.Options);
        left!.ShouldBeEmpty();

        _ = taskId;
        _ = typeId;
    }

    [Fact]
    public async Task Xoa_luat_duyet_thi_cong_mo_lai()
    {
        var (pm, projectId, taskId, typeId, gate) = await SetupAsync();
        var policy = await CreatePolicyAsync(pm.Client, projectId, typeId, gate);

        (await TryMoveAsync(pm.Client, taskId, gate)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var deleted = await pm.Client.DeleteAsync($"/api/v1/approval-policies/{policy.Id}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await TryMoveAsync(pm.Client, taskId, gate)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---------- sửa luật ----------

    [Fact]
    public async Task Sua_luat_doi_duoc_nguoi_duyet_ma_khong_va_khoa_chinh()
    {
        var (pm, projectId, _, typeId, gate) = await SetupAsync();
        var a1 = await CreateUserAsync();
        var a2 = await CreateUserAsync();
        await AddMemberAsync(pm.Client, a1, projectId, RoleInProject.Member);
        await AddMemberAsync(pm.Client, a2, projectId, RoleInProject.Member);

        var policy = await CreatePolicyAsync(pm.Client, projectId, typeId, gate,
            ApproverMode.NamedApprovers, 1, [a1.EmployeeId]);

        // Giữ a1, thêm a2 — chỗ này từng là bẫy "xoá trắng rồi thêm lại" -> vi phạm khoá chính.
        var res = await pm.Client.PutAsJsonAsync($"/api/v1/approval-policies/{policy.Id}",
            new UpdateApprovalPolicyRequest(typeId, gate, ApproverMode.NamedApprovers, 2,
                [a1.EmployeeId, a2.EmployeeId]));

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = (await res.Content.ReadFromJsonAsync<ApprovalPolicyResponse>(TestJson.Options))!;
        updated.MinApprovals.ShouldBe(2);
        updated.Approvers.Count.ShouldBe(2);
        updated.Approvers.ShouldAllBe(a => !string.IsNullOrWhiteSpace(a.Name));
    }
}
