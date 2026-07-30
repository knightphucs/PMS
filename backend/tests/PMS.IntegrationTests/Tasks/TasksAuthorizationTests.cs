using System.Net;
using System.Net.Http.Json;
using PMS.Application.Features.Tasks;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Tasks;

[Collection(IntegrationTestCollection.Name)]
public class TasksAuthorizationTests : IntegrationTestBase
{
    public TasksAuthorizationTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404_chu_khong_phai_403()
    {
        // ADR-019: 403 sẽ xác nhận taskId có tồn tại — đủ để người ngoài dò dữ liệu
        // (OWASP API1:2023). Cùng lý do ADR-006 chọn 404 cho project.
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        (await outsider.Client.GetAsync($"/api/v1/tasks/{taskId}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
        (await outsider.Client.DeleteAsync($"/api/v1/tasks/{taskId}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Task_khong_ton_tai_va_task_ngoai_pham_vi_tra_thong_bao_giong_het_nhau()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var realTaskId = await CreateTaskAsync(pm.Client, projectId);
        var fakeTaskId = Guid.NewGuid();

        var realRes = await outsider.Client.GetAsync($"/api/v1/tasks/{realTaskId}");
        var fakeRes = await outsider.Client.GetAsync($"/api/v1/tasks/{fakeTaskId}");

        var realBody = (await realRes.Content.ReadAsStringAsync()).Replace(realTaskId.ToString(), "ID");
        var fakeBody = (await fakeRes.Content.ReadAsStringAsync()).Replace(fakeTaskId.ToString(), "ID");

        // Chỉ khác nhau ở traceId nên so title, không so nguyên body
        realBody.ShouldContain("Không tìm thấy TaskItem");
        fakeBody.ShouldContain("Không tìm thấy TaskItem");
    }

    [Fact]
    public async Task Member_khong_tao_duoc_task()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);

        var res = await member.Client.PostAsJsonAsync("/api/v1/tasks",
            new CreateTaskRequest("Task lén", projectId, null, null, null, Priority.Medium));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Member_khong_xoa_duoc_task()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        (await member.Client.DeleteAsync($"/api/v1/tasks/{taskId}")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Viewer_doc_duoc_task_nhung_khong_sua_duoc()
    {
        var pm = await CreateUserAsync();
        var viewer = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, viewer, projectId, RoleInProject.Viewer);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var detail = await viewer.Client.GetFromJsonAsync<TaskDetailResponse>($"/api/v1/tasks/{taskId}", TestJson.Options);
        detail.ShouldNotBeNull();

        var update = await viewer.Client.PutAsJsonAsync($"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest("Sửa lén", null, Priority.High, detail!.RowVersion));
        update.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Member_khong_keo_duoc_task_giua_Sprint_va_Backlog()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await member.Client.PutAsJsonAsync($"/api/v1/tasks/{taskId}/sprint",
            new MoveTaskToSprintRequest(null));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
