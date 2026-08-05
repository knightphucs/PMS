using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Features.Tasks;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Tasks;

[Collection(IntegrationTestCollection.Name)]
public class TaskAssignmentTests : IntegrationTestBase
{
    public TaskAssignmentTests(PmsWebApplicationFactory factory) : base(factory) { }

    // ---------- Gán người khác (seq-02) ----------

    [Fact]
    public async Task PM_gan_duoc_thanh_vien_da_Accepted()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/assignees",
            new AssignTaskRequest(member.EmployeeId, RoleInTask.Owner));

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<TaskAssigneeResponse>(TestJson.Options);
        body!.EmployeeId.ShouldBe(member.EmployeeId);
        body.EmployeeName.ShouldNotBeNullOrWhiteSpace();   // navigation phải sẵn sàng khi map
    }

    [Fact]
    public async Task Gan_nguoi_ngoai_project_bi_tu_choi_403()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/assignees",
            new AssignTaskRequest(outsider.EmployeeId, RoleInTask.Owner));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Gan_nguoi_moi_duoc_moi_chua_Accept_bi_tu_choi_403()
    {
        var pm = await CreateUserAsync();
        var pending = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await SeedMemberAsync(projectId, pending.EmployeeId, RoleInProject.Member,
            InvitationStatus.Pending);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/assignees",
            new AssignTaskRequest(pending.EmployeeId, RoleInTask.Owner));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Member_khong_gan_duoc_nguoi_khac()
    {
        var pm = await CreateUserAsync();
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, a, projectId, RoleInProject.Member);
        await InviteAndAcceptAsync(pm.Client, b, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await a.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/assignees",
            new AssignTaskRequest(b.EmployeeId, RoleInTask.Owner));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Mot_task_gan_duoc_nhieu_nguoi()
    {
        var pm = await CreateUserAsync();
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, a, projectId, RoleInProject.Member);
        await InviteAndAcceptAsync(pm.Client, b, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/assignees",
            new AssignTaskRequest(a.EmployeeId, RoleInTask.Owner));
        var second = await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/assignees",
            new AssignTaskRequest(b.EmployeeId, RoleInTask.Contributor));

        // Bản ghi thứ hai từng vi phạm khóa chính vì AddAssignee không sinh Id (đã sửa)
        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        var assignees = await pm.Client.GetFromJsonAsync<List<TaskAssigneeResponse>>(
            $"/api/v1/tasks/{taskId}/assignees", TestJson.Options);
        assignees!.Count.ShouldBe(2);
        assignees.Select(x => x.EmployeeId).ShouldBe([a.EmployeeId, b.EmployeeId], ignoreOrder: true);
    }

    [Fact]
    public async Task Gan_trung_nguoi_bi_chan_409()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/assignees",
            new AssignTaskRequest(member.EmployeeId, RoleInTask.Owner));
        var again = await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/assignees",
            new AssignTaskRequest(member.EmployeeId, RoleInTask.Contributor));

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ---------- Tự nhận ----------

    [Fact]
    public async Task Member_tu_nhan_duoc_task_dang_ToDo_va_PM_nhan_thong_bao()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var pmNotisBefore = await CountNotificationsAsync(pm.EmployeeId);

        var res = await member.Client.PostAsync($"/api/v1/tasks/{taskId}/assignees/me", null);

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        // §5: PM luôn nắm được ai đang làm gì dù không tự tay gán
        (await CountNotificationsAsync(pm.EmployeeId)).ShouldBe(pmNotisBefore + 1);
    }

    [Fact]
    public async Task Tu_nhan_task_khong_o_ToDo_bi_chan_409()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await MoveToColumnAsync(pm.Client, taskId, 1);

        var res = await member.Client.PostAsync($"/api/v1/tasks/{taskId}/assignees/me", null);

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        // ADR-052: thông điệp nói theo TÊN CỘT chứ không theo tên enum — người dùng đặt cột
        // tên gì thì lỗi phải nhắc đúng tên đó, không phải một định danh nội bộ.
        (await res.Content.ReadAsStringAsync()).ShouldContain("chưa bắt đầu");
    }

    [Fact]
    public async Task Viewer_khong_tu_nhan_duoc_task()
    {
        var pm = await CreateUserAsync();
        var viewer = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, viewer, projectId, RoleInProject.Viewer);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await viewer.Client.PostAsync($"/api/v1/tasks/{taskId}/assignees/me", null);

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---------- Gỡ ----------

    [Fact]
    public async Task Nguoi_duoc_gan_tu_rut_duoc_khong_can_PM_duyet()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await member.Client.PostAsync($"/api/v1/tasks/{taskId}/assignees/me", null);

        var res = await member.Client.DeleteAsync(
            $"/api/v1/tasks/{taskId}/assignees/{member.EmployeeId}");

        res.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // TaskAssignment không phải ISoftDeletable -> xóa cứng (nhất quán ADR-012)
        var stillExists = await WithDbAsync(db => db.TaskAssignments
            .IgnoreQueryFilters()
            .AnyAsync(a => a.TaskId == taskId && a.EmployeeId == member.EmployeeId));
        stillExists.ShouldBeFalse();
    }

    [Fact]
    public async Task Member_khong_go_duoc_nguoi_khac()
    {
        var pm = await CreateUserAsync();
        var a = await CreateUserAsync();
        var b = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, a, projectId, RoleInProject.Member);
        await InviteAndAcceptAsync(pm.Client, b, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/assignees",
            new AssignTaskRequest(b.EmployeeId, RoleInTask.Owner));

        var res = await a.Client.DeleteAsync($"/api/v1/tasks/{taskId}/assignees/{b.EmployeeId}");

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Go_nguoi_von_khong_duoc_gan_thi_404()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.DeleteAsync($"/api/v1/tasks/{taskId}/assignees/{member.EmployeeId}");

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Moi_hanh_dong_gan_go_deu_ghi_ActivityLog()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var logsAfterCreate = await CountActivityLogsAsync(taskId);

        await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/assignees",
            new AssignTaskRequest(member.EmployeeId, RoleInTask.Owner));
        await pm.Client.DeleteAsync($"/api/v1/tasks/{taskId}/assignees/{member.EmployeeId}");

        (await CountActivityLogsAsync(taskId)).ShouldBe(logsAfterCreate + 2);
    }
}
