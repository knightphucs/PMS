using System.Net;
using System.Net.Http.Json;
using PMS.Application.Features.Tasks;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Tasks;

/// <summary>
/// Ghim task trên board (2026-08-06) — task ghim luôn đứng ĐẦU cột, cho MỌI người xem
/// project này. Cùng quyền với sửa task (PM), gọi qua HTTP thật để bắt đúng lớp lỗi
/// "service đúng nhưng chưa ai gọi qua endpoint thật" mà dự án đã trả giá nhiều lần.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class TaskPinTests : IntegrationTestBase
{
    public TaskPinTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Ghim_task_dua_no_len_dau_cot_bat_ke_do_uu_tien()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var caoNhat = await CreateTaskAsync(pm.Client, projectId, "Ưu tiên cao", priority: Priority.Highest);
        var thapNhat = await CreateTaskAsync(pm.Client, projectId, "Ưu tiên thấp", priority: Priority.Lowest);

        // Chưa ghim gì: sắp theo Priority — "Ưu tiên cao" đứng trước.
        var truoc = await GetFirstColumnTasksAsync(pm.Client, projectId);
        truoc.Select(t => t.Id).ShouldBe([caoNhat, thapNhat]);

        var res = await pm.Client.PatchAsJsonAsync(
            $"/api/v1/tasks/{thapNhat}/pin", new PinTaskRequest(true));
        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await res.Content.ReadFromJsonAsync<TaskSummaryResponse>(TestJson.Options))!
            .IsPinned.ShouldBeTrue();

        // Đã ghim: "Ưu tiên thấp" nhảy lên đầu dù độ ưu tiên thấp hơn hẳn.
        var sau = await GetFirstColumnTasksAsync(pm.Client, projectId);
        sau.Select(t => t.Id).ShouldBe([thapNhat, caoNhat]);
    }

    [Fact]
    public async Task Go_ghim_tra_task_ve_dung_thu_tu_theo_do_uu_tien()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var caoNhat = await CreateTaskAsync(pm.Client, projectId, "Ưu tiên cao", priority: Priority.Highest);
        var thapNhat = await CreateTaskAsync(pm.Client, projectId, "Ưu tiên thấp", priority: Priority.Lowest);

        await pm.Client.PatchAsJsonAsync($"/api/v1/tasks/{thapNhat}/pin", new PinTaskRequest(true));
        var goGhim = await pm.Client.PatchAsJsonAsync(
            $"/api/v1/tasks/{thapNhat}/pin", new PinTaskRequest(false));
        goGhim.StatusCode.ShouldBe(HttpStatusCode.OK);

        var sau = await GetFirstColumnTasksAsync(pm.Client, projectId);
        sau.Select(t => t.Id).ShouldBe([caoNhat, thapNhat]);
    }

    [Fact]
    public async Task Member_khong_phai_PM_ghim_bi_tu_choi_403()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        // Ghim là hành động quản lý board (ai cũng thấy cùng thứ tự) — cùng quyền UpdateTask,
        // kể cả khi Member đó CHÍNH LÀ assignee của task (khác ChangeStatus, cái đó Member
        // tự làm được với task của mình).
        await pm.Client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/assignees",
            new AssignTaskRequest(member.EmployeeId, RoleInTask.Owner));

        var res = await member.Client.PatchAsJsonAsync(
            $"/api/v1/tasks/{taskId}/pin", new PinTaskRequest(true));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ghim_task_khong_ton_tai_tra_404()
    {
        var pm = await CreateUserAsync();

        var res = await pm.Client.PatchAsJsonAsync(
            $"/api/v1/tasks/{Guid.NewGuid()}/pin", new PinTaskRequest(true));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<List<TaskSummaryResponse>> GetFirstColumnTasksAsync(
        HttpClient client, Guid projectId)
    {
        var board = await client.GetFromJsonAsync<BoardResponse>(
            $"/api/v1/projects/{projectId}/board", TestJson.Options);
        // Bốn cột mặc định (ADR-052), Order 0 = cột trái nhất — nơi task mới luôn rơi vào.
        return board!.Columns.Single(c => c.Column.Order == 0).Tasks.ToList();
    }
}
