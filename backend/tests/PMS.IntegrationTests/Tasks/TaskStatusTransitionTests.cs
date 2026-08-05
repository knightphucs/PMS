using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Features.Tasks;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Tasks;

[Collection(IntegrationTestCollection.Name)]
public class TaskStatusTransitionTests : IntegrationTestBase
{
    public TaskStatusTransitionTests(PmsWebApplicationFactory factory) : base(factory) { }

    // ---------- ADR-017: ai được đổi status ----------

    [Fact]
    public async Task Assignee_doi_duoc_status_task_cua_minh()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await AssignAsync(pm.Client, taskId, member.EmployeeId);

        var res = await member.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/status",
            new ChangeTaskStatusRequest(await ColumnOfTaskAsync(taskId, 1)));

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<TaskSummaryResponse>(TestJson.Options);
        body!.Status.Name.ShouldBe("Đang làm");
    }

    [Fact]
    public async Task ProjectManager_doi_duoc_status_ca_task_khong_do_minh_lam()
    {
        // Đây là điểm ADR-017 nới rộng so với seq-03 (vốn chỉ vẽ actor "Assignee").
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await AssignAsync(pm.Client, taskId, member.EmployeeId);

        var res = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/status",
            new ChangeTaskStatusRequest(await ColumnOfTaskAsync(taskId, 1)));

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Member_khong_phai_assignee_bi_tu_choi_403()
    {
        var pm = await CreateUserAsync();
        var assignee = await CreateUserAsync();
        var buiKhac = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, assignee, projectId, RoleInProject.Member);
        await InviteAndAcceptAsync(pm.Client, buiKhac, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await AssignAsync(pm.Client, taskId, assignee.EmployeeId);

        var res = await buiKhac.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/status",
            new ChangeTaskStatusRequest(await ColumnOfTaskAsync(taskId, 1)));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var status = await WithDbAsync(db => db.Tasks
            .Where(t => t.Id == taskId).Select(t => t.Category).SingleAsync());
        status.ShouldBe(StatusCategory.ToDo);
    }

    [Fact]
    public async Task Viewer_bi_tu_choi_403()
    {
        var pm = await CreateUserAsync();
        var viewer = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, viewer, projectId, RoleInProject.Viewer);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await viewer.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/status",
            new ChangeTaskStatusRequest(await ColumnOfTaskAsync(taskId, 1)));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await outsider.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/status",
            new ChangeTaskStatusRequest(await ColumnOfTaskAsync(taskId, 1)));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------- Chuyển cột (ADR-052 thay thế ma trận chuyển trạng thái của ADR-021) ----------

    [Fact]
    public async Task Nhay_thang_tu_cot_dau_sang_cot_cuoi_la_hop_le()
    {
        // 🔄 Test này TRƯỚC ĐÂY khẳng định điều ngược lại (`Nhay_thang_ToDo_sang_Done_bi_chan_409`).
        // Đảo chiều là có chủ đích, không phải nới lỏng cho dễ: với cột do NGƯỜI DÙNG tạo thì
        // hệ thống không còn cơ sở nào để nói cặp nào hợp lệ — nó không biết "Chờ QA" đứng
        // trước hay sau "Đang sửa". Ép một luật lên đó là đoán hộ quy trình của người khác.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/status",
            new ChangeTaskStatusRequest(await ColumnOfTaskAsync(taskId, 3)));

        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await pm.Client.GetFromJsonAsync<TaskDetailResponse>(
            $"/api/v1/tasks/{taskId}", TestJson.Options);
        detail!.Status.Name.ShouldBe("Hoàn thành");
        detail.Status.Category.ShouldBe(StatusCategory.Done);
    }

    [Fact]
    public async Task Doi_ve_dung_cot_dang_dung_tra_200_chu_khong_con_409()
    {
        // 🔄 Cũng đảo chiều so với `Doi_status_khong_can_RowVersion_nhung_lan_hai_cung_dich_bi_chan_409`.
        // ADR-021 từng dùng chính đặc điểm "đứng yên là lỗi" làm chốt chặn concurrency thay
        // cho RowVersion. Sau ADR-052 chốt đó không còn — và đó là đánh đổi được ghi nhận:
        // đổi trạng thái là thao tác idempotent, hai người cùng kéo về một cột thì kết quả
        // giống hệt nhau nên không có gì để tranh chấp.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var doing = await ColumnOfTaskAsync(taskId, 1);

        var first = await pm.Client.PatchAsJsonAsync(
            $"/api/v1/tasks/{taskId}/status", new ChangeTaskStatusRequest(doing));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await pm.Client.PatchAsJsonAsync(
            $"/api/v1/tasks/{taskId}/status", new ChangeTaskStatusRequest(doing));
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cot_cua_project_khac_tra_404()
    {
        // 404 chứ không 409: cột của project khác thì với người gọi nó không tồn tại. Trả
        // 409 sẽ xác nhận "id này có thật, chỉ là không thuộc project của bạn" (ADR-019).
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var otherProjectId = await CreateProjectAsync(pm.Client, "Project khác");
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        var foreignColumn = await ColumnIdAsync(otherProjectId, 1);

        var res = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/status",
            new ChangeTaskStatusRequest(foreignColumn));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------- Blocker (TaskLink) ----------

    [Fact]
    public async Task Task_bi_chan_boi_task_chua_Done_thi_khong_vao_duoc_InProgress()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var blockerId = await CreateTaskAsync(pm.Client, projectId, "Task chặn");
        var blockedId = await CreateTaskAsync(pm.Client, projectId, "Task bị chặn");
        await LinkAsync(blockerId, blockedId, LinkType.Blocks);

        var res = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{blockedId}/status",
            new ChangeTaskStatusRequest(await ColumnOfTaskAsync(blockedId, 1)));

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).ShouldContain("Task chặn");
    }

    [Fact]
    public async Task Task_chan_da_Done_thi_task_bi_chan_di_tiep_duoc()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var blockerId = await CreateTaskAsync(pm.Client, projectId, "Task chặn");
        var blockedId = await CreateTaskAsync(pm.Client, projectId, "Task bị chặn");
        await LinkAsync(blockerId, blockedId, LinkType.Blocks);

        await MoveToColumnAsync(pm.Client, blockerId, 3);

        var res = await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{blockedId}/status",
            new ChangeTaskStatusRequest(await ColumnOfTaskAsync(blockedId, 1)));

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---------- ActivityLog & Notification ----------

    [Fact]
    public async Task Doi_status_ghi_ActivityLog_va_bao_cho_Reporter()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);       // pm là Reporter
        await AssignAsync(pm.Client, taskId, member.EmployeeId);

        var logsBefore = await CountActivityLogsAsync(taskId);
        var pmNotisBefore = await CountNotificationsAsync(pm.EmployeeId);
        var memberNotisBefore = await CountNotificationsAsync(member.EmployeeId);

        await member.Client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/status",
            new ChangeTaskStatusRequest(await ColumnOfTaskAsync(taskId, 1)));

        (await CountActivityLogsAsync(taskId)).ShouldBe(logsBefore + 1);
        (await CountNotificationsAsync(pm.EmployeeId)).ShouldBe(pmNotisBefore + 1);

        // Người thực hiện không tự nhận thông báo về hành động của chính mình. Đếm theo
        // delta chứ không theo con số tuyệt đối: member đã có sẵn thông báo InvitedToProject
        // và TaskAssigned từ các bước dựng dữ liệu.
        (await CountNotificationsAsync(member.EmployeeId)).ShouldBe(memberNotisBefore);
    }

    // ---------- helpers ----------

    private static async Task AssignAsync(HttpClient pmClient, Guid taskId, Guid employeeId)
    {
        var res = await pmClient.PostAsJsonAsync($"/api/v1/tasks/{taskId}/assignees",
            new AssignTaskRequest(employeeId, RoleInTask.Owner));
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// TaskLink chưa có API (nằm ngoài phạm vi đợt này) nên seed thẳng DB qua domain method.
    /// </summary>
    private Task LinkAsync(Guid sourceId, Guid targetId, LinkType linkType)
        => WithDbAsync(async db =>
        {
            var source = await db.Tasks.SingleAsync(t => t.Id == sourceId);
            var target = await db.Tasks.SingleAsync(t => t.Id == targetId);
            source.LinkTo(target, linkType);
            await db.SaveChangesAsync();
        });
}
