using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Models;
using PMS.Application.Features.Tasks;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Tasks;

[Collection(IntegrationTestCollection.Name)]
public class TasksCrudTests : IntegrationTestBase
{
    public TasksCrudTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Tao_task_thi_nguoi_tao_thanh_Reporter_va_task_bat_dau_o_ToDo()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var taskId = await CreateTaskAsync(pm.Client, projectId, "Dựng API");

        var detail = await pm.Client.GetFromJsonAsync<TaskDetailResponse>($"/api/v1/tasks/{taskId}", TestJson.Options);
        detail!.Status.ShouldBe(Status.ToDo);
        detail.ReporterId.ShouldBe(pm.EmployeeId);
        detail.ProjectId.ShouldBe(projectId);
        detail.Assignees.ShouldBeEmpty();
        detail.RowVersion.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Sua_task_voi_RowVersion_cu_thi_bi_chan_409()
    {
        // ADR-016/021: chỉ có cột RowVersion là chưa đủ, client phải round-trip đúng token.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var detail = await pm.Client.GetFromJsonAsync<TaskDetailResponse>($"/api/v1/tasks/{taskId}", TestJson.Options);
        var staleRowVersion = detail!.RowVersion;

        var first = await pm.Client.PutAsJsonAsync($"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest("Lần sửa 1", null, Priority.High, staleRowVersion));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await pm.Client.PutAsJsonAsync($"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest("Lần sửa 2", null, Priority.Low, staleRowVersion));

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var after = await pm.Client.GetFromJsonAsync<TaskDetailResponse>($"/api/v1/tasks/{taskId}", TestJson.Options);
        after!.Name.ShouldBe("Lần sửa 1");   // lần 2 không được ghi đè
    }

    [Fact]
    public async Task Sua_task_thieu_RowVersion_bi_ValidationFilter_chan_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var res = await pm.Client.PutAsJsonAsync($"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest("Tên mới", null, Priority.High, []));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Xoa_task_khong_co_subtask_thanh_cong_va_chi_xoa_mem()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        (await pm.Client.DeleteAsync($"/api/v1/tasks/{taskId}")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        (await pm.Client.GetAsync($"/api/v1/tasks/{taskId}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);

        var stillInDb = await WithDbAsync(db => db.Tasks
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == taskId && t.IsDeleted));
        stillInDb.ShouldBeTrue();
    }

    [Fact]
    public async Task Danh_sach_task_cua_project_khong_liet_ke_subtask()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var parentId = await CreateTaskAsync(pm.Client, projectId, "Task cha");
        await CreateTaskAsync(pm.Client, projectId, "Subtask", parentTaskId: parentId);

        var paged = await pm.Client.GetFromJsonAsync<PagedResult<TaskSummaryResponse>>(
            $"/api/v1/projects/{projectId}/tasks", TestJson.Options);

        // Subtask hiện trong chi tiết task cha, không phải mục riêng ở danh sách
        paged!.Items.ShouldHaveSingleItem().Id.ShouldBe(parentId);
        paged.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Tao_task_ghi_ActivityLog()
    {
        // ADR-013 chấp nhận rủi ro "có thể quên gọi logger" -> mỗi action phải có test
        // khẳng định số dòng ActivityLogs tăng đúng.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var taskId = await CreateTaskAsync(pm.Client, projectId);

        (await CountActivityLogsAsync(taskId)).ShouldBe(1);
    }
}
