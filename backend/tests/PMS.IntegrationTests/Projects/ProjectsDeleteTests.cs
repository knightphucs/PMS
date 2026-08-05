using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Models;
using PMS.Application.Features.Projects;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Projects;

[Collection(IntegrationTestCollection.Name)]
public class ProjectsDeleteTests : IntegrationTestBase
{
    public ProjectsDeleteTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact] // KB13
    public async Task Xoa_project_khong_co_task_thanh_cong_va_bien_khoi_moi_query()
    {
        var a = await CreateUserAsync();
        var id = await CreateProjectAsync(a.Client);

        (await a.Client.DeleteAsync($"/api/v1/Projects/{id}")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        (await a.Client.GetAsync($"/api/v1/Projects/{id}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);

        var paged = await a.Client.GetFromJsonAsync<PagedResult<ProjectSummaryResponse>>(
            "/api/v1/Projects", TestJson.Options);
        paged!.Items.ShouldBeEmpty();

        // Soft delete, KHÔNG xóa cứng -> hàng vẫn còn trong DB
        var exists = await WithDbAsync(db => db.Projects
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Id == id && p.IsDeleted));
        exists.ShouldBeTrue();
    }

    [Fact] // KB14
    public async Task Con_task_chua_Done_thi_bi_chan_409_va_khong_xoa_gi
        ()
    {
        var a = await CreateUserAsync();
        var id = await CreateProjectAsync(a.Client);
        var taskId = await SeedTaskAsync(id, a.EmployeeId, 1);

        var res = await a.Client.DeleteAsync($"/api/v1/Projects/{id}");

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).ShouldContain("1");

        // Guard chặn -> KHÔNG được ghi gì. Kiểm tra cả project và task.
        var state = await WithDbAsync(db => db.Projects
            .IgnoreQueryFilters()
            .Where(p => p.Id == id)
            .Select(p => new { p.IsDeleted, TaskDeleted = db.Tasks
                .IgnoreQueryFilters().Any(t => t.Id == taskId && t.IsDeleted) })
            .SingleAsync());

        state.IsDeleted.ShouldBeFalse();
        state.TaskDeleted.ShouldBeFalse();
    }

    [Fact] // KB15 + KB16 — bằng chứng cho bước 1 và ADR-008
    public async Task Cascade_xuong_task_va_sprint_voi_cung_mot_moc_DeletedAt()
    {
        var a = await CreateUserAsync();
        var id = await CreateProjectAsync(a.Client);
        var taskId = await SeedTaskAsync(id, a.EmployeeId, 3);

        var sprintId = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.Sprints.Add(new Sprint
            {
                Id = sprintId, Name = "Sprint 1", Goal = "Test", ProjectId = id,
                StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(14)
            });
            await db.SaveChangesAsync();
        });

        (await a.Client.DeleteAsync($"/api/v1/Projects/{id}")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        // IgnoreQueryFilters bắt buộc: cả 3 hàng đều IsDeleted = true nên query thường
        // sẽ không thấy gì và test sẽ "fail vì null" chứ không phải vì logic sai.
        var projectStamp = await WithDbAsync(db => db.Projects.IgnoreQueryFilters()
            .Where(p => p.Id == id).Select(p => p.DeletedAt).SingleOrDefaultAsync());
        var taskStamp = await WithDbAsync(db => db.Tasks.IgnoreQueryFilters()
            .Where(t => t.Id == taskId).Select(t => t.DeletedAt).SingleOrDefaultAsync());
        var sprintRow = await WithDbAsync(db => db.Sprints.IgnoreQueryFilters()
            .Where(s => s.Id == sprintId)
            .Select(s => new { s.IsDeleted, s.DeletedAt }).SingleOrDefaultAsync());

        // Tách assertion "hàng còn tồn tại" khỏi assertion "mốc thời gian khớp nhau"
        // -> đọc thông báo lỗi là biết ngay xóa cứng hay cascade sai.
        sprintRow.ShouldNotBeNull("Sprint bị XÓA CỨNG — kiểm tra Sprint có implement ISoftDeletable chưa");
        sprintRow!.IsDeleted.ShouldBeTrue();

        projectStamp.ShouldNotBeNull();
        taskStamp.ShouldBe(projectStamp);
        sprintRow.DeletedAt.ShouldBe(projectStamp);
    }

    [Fact] // bổ sung: task đã bị xóa lẻ trước đó giữ nguyên mốc cũ
    public async Task Task_bi_xoa_truoc_do_khong_bi_dong_dau_lai()
    {
        var a = await CreateUserAsync();
        var id = await CreateProjectAsync(a.Client);
        var oldTaskId = await SeedTaskAsync(id, a.EmployeeId, 3);

        // Xóa lẻ task trước, bằng chính interceptor
        await WithDbAsync(async db =>
        {
            var t = await db.Tasks.SingleAsync(x => x.Id == oldTaskId);
            db.Tasks.Remove(t);
            await db.SaveChangesAsync();
        });

        var oldStamp = await WithDbAsync(db => db.Tasks.IgnoreQueryFilters()
            .Where(t => t.Id == oldTaskId).Select(t => t.DeletedAt).SingleAsync());

        await Task.Delay(50);   // đảm bảo mốc xóa project khác mốc cũ
        await a.Client.DeleteAsync($"/api/v1/Projects/{id}");

        var afterStamp = await WithDbAsync(db => db.Tasks.IgnoreQueryFilters()
            .Where(t => t.Id == oldTaskId).Select(t => t.DeletedAt).SingleAsync());

        // GetForDeletionAsync nạp project.Tasks qua query filter -> task đã xóa KHÔNG
        // được nạp lại -> giữ nguyên DeletedAt cũ. Điều kiện cần nếu sau này làm
        // restore theo lô (phân biệt "xóa cùng project" với "xóa lẻ trước đó").
        afterStamp.ShouldBe(oldStamp);
    }
}