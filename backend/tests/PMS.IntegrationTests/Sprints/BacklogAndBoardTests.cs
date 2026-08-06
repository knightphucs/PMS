using System.Net;
using System.Net.Http.Json;
using PMS.Application.Features.Tasks;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Sprints;

[Collection(IntegrationTestCollection.Name)]
public class BacklogAndBoardTests : IntegrationTestBase
{
    public BacklogAndBoardTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Backlog_chi_gom_task_chua_gan_sprint()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var sprintId = await CreateSprintAsync(pm.Client, projectId);
        var trongBacklog = await CreateTaskAsync(pm.Client, projectId, "Chưa xếp");
        await CreateTaskAsync(pm.Client, projectId, "Đã xếp sprint", sprintId: sprintId);

        var backlog = await pm.Client.GetFromJsonAsync<List<TaskSummaryResponse>>(
            $"/api/v1/projects/{projectId}/backlog", TestJson.Options);

        backlog!.ShouldHaveSingleItem().Id.ShouldBe(trongBacklog);
    }

    [Fact]
    public async Task Keo_task_tu_Backlog_vao_Sprint_va_nguoc_lai()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var sprintId = await CreateSprintAsync(pm.Client, projectId);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var vaoSprint = await pm.Client.PutAsJsonAsync($"/api/v1/tasks/{taskId}/sprint",
            new MoveTaskToSprintRequest(sprintId));
        vaoSprint.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await pm.Client.GetFromJsonAsync<List<TaskSummaryResponse>>(
            $"/api/v1/projects/{projectId}/backlog", TestJson.Options))!.ShouldBeEmpty();

        var veBacklog = await pm.Client.PutAsJsonAsync($"/api/v1/tasks/{taskId}/sprint",
            new MoveTaskToSprintRequest(null));
        veBacklog.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await pm.Client.GetFromJsonAsync<List<TaskSummaryResponse>>(
            $"/api/v1/projects/{projectId}/backlog", TestJson.Options))!.ShouldHaveSingleItem().Id.ShouldBe(taskId);
    }

    [Fact]
    public async Task Keo_task_vao_sprint_cua_project_khac_bi_chan_400()
    {
        var pm = await CreateUserAsync();
        var projectA = await CreateProjectAsync(pm.Client, "Project A");
        var projectB = await CreateProjectAsync(pm.Client, "Project B");
        var sprintB = await CreateSprintAsync(pm.Client, projectB);
        var taskA = await CreateTaskAsync(pm.Client, projectA);

        var res = await pm.Client.PutAsJsonAsync($"/api/v1/tasks/{taskA}/sprint",
            new MoveTaskToSprintRequest(sprintB));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Board_luon_tra_du_4_cot_ke_ca_project_rong()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var board = await pm.Client.GetFromJsonAsync<BoardResponse>(
            $"/api/v1/projects/{projectId}/board", TestJson.Options);

        board!.Columns.Count.ShouldBe(4);
        // ADR-052: cột do project cấu hình, trả theo Order trái->phải. Bốn cột mặc định
        // giữ đúng ý nghĩa bốn trạng thái cũ nên test cũ đọc vẫn hiểu ngay.
        board.Columns.Select(c => c.Column.Name).ShouldBe(
            ["Cần làm", "Đang làm", "Đang duyệt", "Hoàn thành"]);
        board.Columns.ShouldAllBe(c => c.Tasks.Count == 0);
    }

    [Fact]
    public async Task Board_xep_task_dung_cot_theo_Status()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var todo = await CreateTaskAsync(pm.Client, projectId, "Chưa làm");
        var inProgress = await CreateTaskAsync(pm.Client, projectId, "Đang làm");
        await MoveToColumnAsync(pm.Client, inProgress, 1);

        var board = await pm.Client.GetFromJsonAsync<BoardResponse>(
            $"/api/v1/projects/{projectId}/board", TestJson.Options);

        board!.Columns.Single(c => c.Column.Name == "Cần làm")
             .Tasks.ShouldHaveSingleItem().Id.ShouldBe(todo);
        board.Columns.Single(c => c.Column.Name == "Đang làm")
             .Tasks.ShouldHaveSingleItem().Id.ShouldBe(inProgress);
        board.Columns.Single(c => c.Column.Name == "Hoàn thành").Tasks.ShouldBeEmpty();
    }

    [Fact]
    public async Task Board_theo_sprint_chi_lay_task_cua_sprint_do()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var sprintId = await CreateSprintAsync(pm.Client, projectId);
        var trongSprint = await CreateTaskAsync(pm.Client, projectId, "Trong sprint", sprintId: sprintId);
        await CreateTaskAsync(pm.Client, projectId, "Ngoài sprint");

        var board = await pm.Client.GetFromJsonAsync<BoardResponse>(
            $"/api/v1/projects/{projectId}/board?sprintId={sprintId}", TestJson.Options);

        board!.SprintId.ShouldBe(sprintId);
        board.Columns.SelectMany(c => c.Tasks).ShouldHaveSingleItem().Id.ShouldBe(trongSprint);
    }

    [Fact]
    public async Task Board_khong_hien_subtask_thanh_the_rieng()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var parentId = await CreateTaskAsync(pm.Client, projectId, "Task cha");
        await CreateTaskAsync(pm.Client, projectId, "Subtask", parentTaskId: parentId);

        var board = await pm.Client.GetFromJsonAsync<BoardResponse>(
            $"/api/v1/projects/{projectId}/board", TestJson.Options);

        board!.Columns.SelectMany(c => c.Tasks).ShouldHaveSingleItem().Id.ShouldBe(parentId);
    }

    [Fact]
    public async Task The_tren_Board_mang_theo_danh_sach_nguoi_dam_nhan()
    {
        // Không có dữ liệu này thì client phải gọi N+1 lần /tasks/{id}/assignees chỉ để
        // vẽ avatar, và không có cách nào biết "tôi có phải assignee không" để gác quyền
        // đổi status theo ADR-017.
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var gan = await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/assignees",
            new AssignTaskRequest(member.EmployeeId, RoleInTask.Owner));
        gan.StatusCode.ShouldBe(HttpStatusCode.OK);

        var board = await pm.Client.GetFromJsonAsync<BoardResponse>(
            $"/api/v1/projects/{projectId}/board", TestJson.Options);

        var the = board!.Columns.SelectMany(c => c.Tasks).ShouldHaveSingleItem();
        var nguoiLam = the.Assignees.ShouldHaveSingleItem();
        nguoiLam.EmployeeId.ShouldBe(member.EmployeeId);
        nguoiLam.EmployeeName.ShouldNotBeNullOrWhiteSpace();

        // Backlog đi qua một query khác — phải kiểm riêng, không suy ra từ board.
        var backlog = await pm.Client.GetFromJsonAsync<List<TaskSummaryResponse>>(
            $"/api/v1/projects/{projectId}/backlog", TestJson.Options);
        backlog!.ShouldHaveSingleItem().Assignees.ShouldHaveSingleItem().EmployeeId.ShouldBe(member.EmployeeId);
    }

    [Fact]
    public async Task The_tren_Board_tinh_dung_SubtaskProgress()
    {
        // SubtaskProgress đọc Subtasks.Count. Thiếu Include thì collection rỗng và giá
        // trị LUÔN là 0 — sai im lặng, không có lỗi nào chỉ ra.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var parentId = await CreateTaskAsync(pm.Client, projectId, "Task cha");
        var sub1 = await CreateTaskAsync(pm.Client, projectId, "Sub 1", parentTaskId: parentId);
        await CreateTaskAsync(pm.Client, projectId, "Sub 2", parentTaskId: parentId);

        await MoveToColumnAsync(pm.Client, sub1, 3);

        var board = await pm.Client.GetFromJsonAsync<BoardResponse>(
            $"/api/v1/projects/{projectId}/board", TestJson.Options);

        board!.Columns.SelectMany(c => c.Tasks)
              .ShouldHaveSingleItem()
              .SubtaskProgress.ShouldBe(50m);
    }

    [Fact]
    public async Task Viewer_xem_duoc_Board_va_Backlog()
    {
        var pm = await CreateUserAsync();
        var viewer = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, viewer, projectId, RoleInProject.Viewer);

        (await viewer.Client.GetAsync($"/api/v1/projects/{projectId}/board")).StatusCode
            .ShouldBe(HttpStatusCode.OK);
        (await viewer.Client.GetAsync($"/api/v1/projects/{projectId}/backlog")).StatusCode
            .ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404_khi_xem_Board()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        (await outsider.Client.GetAsync($"/api/v1/projects/{projectId}/board")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }
}
