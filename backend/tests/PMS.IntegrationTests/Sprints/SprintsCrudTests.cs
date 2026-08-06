using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Features.Sprints;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Sprints;

[Collection(IntegrationTestCollection.Name)]
public class SprintsCrudTests : IntegrationTestBase
{
    public SprintsCrudTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task PM_tao_va_doc_duoc_sprint()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var sprintId = await CreateSprintAsync(pm.Client, projectId, "Sprint 1");

        var sprint = await pm.Client.GetFromJsonAsync<SprintResponse>($"/api/v1/sprints/{sprintId}", TestJson.Options);
        sprint!.Name.ShouldBe("Sprint 1");
        sprint.ProjectId.ShouldBe(projectId);
        sprint.TaskCount.ShouldBe(0);
    }

    [Fact]
    public async Task IsActive_dung_khi_hom_nay_nam_trong_khoang_sprint()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var dangChay = await CreateSprintAsync(pm.Client, projectId, "Đang chạy", -3, 7);
        var chuaBatDau = await CreateSprintAsync(pm.Client, projectId, "Chưa bắt đầu", 10, 24);

        var sprints = await pm.Client.GetFromJsonAsync<List<SprintResponse>>(
            $"/api/v1/projects/{projectId}/sprints", TestJson.Options);

        sprints.ShouldNotBeNull();
        sprints!.Single(s => s.Id == dangChay).IsActive.ShouldBeTrue();
        sprints.Single(s => s.Id == chuaBatDau).IsActive.ShouldBeFalse();
    }

    /// <summary>
    /// 🔴 Bẫy thật (phát hiện 2026-08-06, qua báo cáo người dùng): trước khi có
    /// <c>IsOverdue</c>, frontend suy "quá hạn" từ <c>!IsActive</c> — sai, vì
    /// <c>IsActive = false</c> cũng đúng khi sprint được bấm Start SỚM hơn kế hoạch
    /// (<c>StartDate</c> còn ở tương lai), một tình huống NGƯỢC HẲN với quá hạn.
    /// </summary>
    [Fact]
    public async Task IsOverdue_chi_dung_khi_qua_EndDate_KHONG_dung_khi_chay_som_hon_ke_hoach()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        // Bấm "Bắt đầu" SỚM: StartDate còn cách 5 ngày nữa mới tới, nhưng Status đã Active.
        var chaySom = await CreateSprintAsync(pm.Client, projectId, "Chạy sớm", 5, 14);
        (await pm.Client.PostAsync($"/api/v1/sprints/{chaySom}/start", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var sprints = await pm.Client.GetFromJsonAsync<List<SprintResponse>>(
            $"/api/v1/projects/{projectId}/sprints", TestJson.Options);

        var response = sprints!.Single(s => s.Id == chaySom);
        response.IsActive.ShouldBeFalse();    // đúng: hôm nay chưa tới StartDate
        response.IsOverdue.ShouldBeFalse();   // nhưng KHÔNG phải quá hạn — ngược lại là sớm
    }

    [Fact]
    public async Task IsOverdue_dung_khi_da_qua_EndDate_ma_chua_dong_so()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var quaHan = await CreateSprintAsync(pm.Client, projectId, "Quá hạn", -10, -1);
        (await pm.Client.PostAsync($"/api/v1/sprints/{quaHan}/start", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var sprints = await pm.Client.GetFromJsonAsync<List<SprintResponse>>(
            $"/api/v1/projects/{projectId}/sprints", TestJson.Options);

        sprints!.Single(s => s.Id == quaHan).IsOverdue.ShouldBeTrue();
    }

    [Fact]
    public async Task EndDate_truoc_StartDate_bi_ValidationFilter_chan_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await pm.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/sprints",
            new CreateSprintRequest("Sprint lỗi", "Mục tiêu",
                DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(3)));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Member_khong_tao_duoc_sprint()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);

        var res = await member.Client.PostAsJsonAsync($"/api/v1/projects/{projectId}/sprints",
            new CreateSprintRequest("Sprint lén", "Mục tiêu",
                DateTime.UtcNow, DateTime.UtcNow.AddDays(14)));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Nguoi_ngoai_project_nhan_404_khi_doc_sprint()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var sprintId = await CreateSprintAsync(pm.Client, projectId);

        var res = await outsider.Client.GetAsync($"/api/v1/sprints/{sprintId}");

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        // ADR-019: thông báo phải nói về Sprint, không rò rỉ project nào chứa nó
        (await res.Content.ReadAsStringAsync()).ShouldContain("Sprint");
    }

    // ---------- ADR-020 ----------

    [Fact]
    public async Task Xoa_sprint_day_task_ve_Backlog_chu_khong_xoa_task()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var sprintId = await CreateSprintAsync(pm.Client, projectId);
        var taskId = await CreateTaskAsync(pm.Client, projectId, "Task trong sprint", sprintId: sprintId);

        (await pm.Client.DeleteAsync($"/api/v1/sprints/{sprintId}")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        var task = await WithDbAsync(db => db.Tasks
            .IgnoreQueryFilters()
            .Where(t => t.Id == taskId)
            .Select(t => new { t.IsDeleted, t.SprintId })
            .SingleAsync());

        task.IsDeleted.ShouldBeFalse();     // task sống sót
        task.SprintId.ShouldBeNull();       // và về Backlog

        // Sprint xóa mềm, không xóa cứng
        var sprintDeleted = await WithDbAsync(db => db.Sprints
            .IgnoreQueryFilters()
            .AnyAsync(s => s.Id == sprintId && s.IsDeleted));
        sprintDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Task_sau_khi_xoa_sprint_xuat_hien_o_Backlog()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var sprintId = await CreateSprintAsync(pm.Client, projectId);
        var taskId = await CreateTaskAsync(pm.Client, projectId, "Task", sprintId: sprintId);

        var backlogTruoc = await pm.Client.GetFromJsonAsync<List<TaskSummary>>(
            $"/api/v1/projects/{projectId}/backlog", TestJson.Options);
        backlogTruoc!.ShouldBeEmpty();

        await pm.Client.DeleteAsync($"/api/v1/sprints/{sprintId}");

        var backlogSau = await pm.Client.GetFromJsonAsync<List<TaskSummary>>(
            $"/api/v1/projects/{projectId}/backlog", TestJson.Options);
        backlogSau!.ShouldHaveSingleItem().Id.ShouldBe(taskId);
    }

    [Fact]
    public async Task Member_khong_xoa_duoc_sprint()
    {
        var pm = await CreateUserAsync();
        var member = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await InviteAndAcceptAsync(pm.Client, member, projectId, RoleInProject.Member);
        var sprintId = await CreateSprintAsync(pm.Client, projectId);

        (await member.Client.DeleteAsync($"/api/v1/sprints/{sprintId}")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>Chỉ cần Id để khẳng định task nào nằm trong Backlog.</summary>
    private record TaskSummary(Guid Id);
}
