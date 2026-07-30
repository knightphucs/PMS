using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Features.Tasks;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Tasks;

[Collection(IntegrationTestCollection.Name)]
public class SubtaskTests : IntegrationTestBase
{
    public SubtaskTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Subtask_thua_ke_ProjectId_cua_task_cha_va_hien_trong_chi_tiet_cha()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var parentId = await CreateTaskAsync(pm.Client, projectId, "Task cha");
        var subId = await CreateTaskAsync(pm.Client, projectId, "Subtask", parentTaskId: parentId);

        var parent = await pm.Client.GetFromJsonAsync<TaskDetailResponse>($"/api/v1/tasks/{parentId}");
        parent!.Subtasks.ShouldHaveSingleItem().Id.ShouldBe(subId);

        var sub = await pm.Client.GetFromJsonAsync<TaskDetailResponse>($"/api/v1/tasks/{subId}");
        sub!.ParentTaskId.ShouldBe(parentId);
        sub.ProjectId.ShouldBe(projectId);
    }

    [Fact]
    public async Task Subtask_la_task_day_du_co_status_va_assignee_rieng()
    {
        // §5: Subtask KHÔNG phải checklist item — có Status riêng theo state machine
        // và Assignee riêng, có thể khác task cha.
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var parentId = await CreateTaskAsync(pm.Client, projectId, "Task cha");
        var subId = await CreateTaskAsync(pm.Client, projectId, "Subtask", parentTaskId: parentId);

        await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{subId}/assignees",
            new AssignTaskRequest(member.EmployeeId, RoleInTask.Owner));
        await AdvanceStatusAsync(member.Client, subId, Status.InProgress);

        var sub = await pm.Client.GetFromJsonAsync<TaskDetailResponse>($"/api/v1/tasks/{subId}");
        sub!.Status.ShouldBe(Status.InProgress);
        sub.Assignees.ShouldHaveSingleItem().EmployeeId.ShouldBe(member.EmployeeId);

        var parent = await pm.Client.GetFromJsonAsync<TaskDetailResponse>($"/api/v1/tasks/{parentId}");
        parent!.Status.ShouldBe(Status.ToDo);          // task cha độc lập
        parent.Assignees.ShouldBeEmpty();
    }

    [Fact]
    public async Task Tao_subtask_cua_subtask_bi_chan_409()
    {
        // Giới hạn 1 cấp cha–con, enforce ở domain (Task.AddSubtask). Trả 409 nhờ
        // DomainException chứ không phải 500 (ADR-011).
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var parentId = await CreateTaskAsync(pm.Client, projectId, "Task cha");
        var subId = await CreateTaskAsync(pm.Client, projectId, "Subtask", parentTaskId: parentId);

        var res = await pm.Client.PostAsJsonAsync("/api/v1/tasks",
            new CreateTaskRequest("Cháu", projectId, null, subId, null, Priority.Medium));

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Tao_subtask_voi_task_cha_o_project_khac_bi_chan_400()
    {
        var pm = await CreateUserAsync();
        var projectA = await CreateProjectAsync(pm.Client, "Project A");
        var projectB = await CreateProjectAsync(pm.Client, "Project B");
        var parentInA = await CreateTaskAsync(pm.Client, projectA, "Task cha ở A");

        var res = await pm.Client.PostAsJsonAsync("/api/v1/tasks",
            new CreateTaskRequest("Subtask ở B", projectB, null, parentInA, null, Priority.Medium));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubtaskProgress_tinh_theo_ty_le_subtask_da_Done()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var parentId = await CreateTaskAsync(pm.Client, projectId, "Task cha");
        var sub1 = await CreateTaskAsync(pm.Client, projectId, "Sub 1", parentTaskId: parentId);
        await CreateTaskAsync(pm.Client, projectId, "Sub 2", parentTaskId: parentId);

        await AdvanceStatusAsync(pm.Client, sub1, Status.Done);

        var parent = await pm.Client.GetFromJsonAsync<TaskDetailResponse>($"/api/v1/tasks/{parentId}");
        parent!.SubtaskProgress.ShouldBe(50m);
        // Task cha KHÔNG tự động Done — Jira behavior, §5
        parent.Status.ShouldBe(Status.ToDo);
    }

    // ---------- ADR-018 ----------

    [Fact]
    public async Task Xoa_task_con_subtask_chua_Done_bi_chan_409_va_khong_xoa_gi()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var parentId = await CreateTaskAsync(pm.Client, projectId, "Task cha");
        var subId = await CreateTaskAsync(pm.Client, projectId, "Subtask", parentTaskId: parentId);

        var res = await pm.Client.DeleteAsync($"/api/v1/tasks/{parentId}");

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).ShouldContain("1");

        // Guard chặn -> tuyệt đối không ghi gì
        var state = await WithDbAsync(db => db.Tasks
            .IgnoreQueryFilters()
            .Where(t => t.Id == parentId || t.Id == subId)
            .Select(t => t.IsDeleted)
            .ToListAsync());
        state.ShouldAllBe(deleted => !deleted);
        _ = subId;
    }

    [Fact]
    public async Task Xoa_task_co_subtask_da_Done_thi_cascade_xuong_subtask()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var parentId = await CreateTaskAsync(pm.Client, projectId, "Task cha");
        var subId = await CreateTaskAsync(pm.Client, projectId, "Subtask", parentTaskId: parentId);
        await AdvanceStatusAsync(pm.Client, subId, Status.Done);

        (await pm.Client.DeleteAsync($"/api/v1/tasks/{parentId}")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        // Cascade tường minh: nếu quên, subtask sẽ thành "mồ côi" mà query trực tiếp
        // vẫn nhìn thấy (bài học ADR-008).
        var subDeleted = await WithDbAsync(db => db.Tasks
            .IgnoreQueryFilters()
            .Where(t => t.Id == subId)
            .Select(t => t.IsDeleted)
            .SingleAsync());
        subDeleted.ShouldBeTrue();
    }
}
