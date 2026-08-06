using System.Net;
using System.Net.Http.Json;
using PMS.Application.Features.Reports;
using PMS.Application.Features.Sprints;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Reports;

/// <summary>
/// Nhóm báo cáo kiểu Jira (ADR — §1 hạng mục 11): backlog insight + velocity.
///
/// 🔴 <c>StatisticsService</c> từng trả 500 ở MỌI lần gọi suốt một ngày vì không có test
/// nào chạm tới lúc viết (xem ARCHITECTURE.md). Bộ test này tồn tại chính để không lặp lại
/// đúng lớp lỗi đó — mỗi endpoint được gọi qua HTTP thật, không chỉ qua service đã mock.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ReportsTests : IntegrationTestBase
{
    public ReportsTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetBacklogInsight_dem_dung_va_khong_can_truyen_horizon()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        await CreateTaskAsync(pm.Client, projectId, "Quá hạn", dueDate: DateTime.UtcNow.AddDays(-2));
        await CreateTaskAsync(pm.Client, projectId, "Không hạn");

        var res = await pm.Client.GetAsync($"/api/v1/projects/{projectId}/reports/backlog-insight");
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<BacklogInsightResponse>(TestJson.Options);
        body!.TotalOpen.ShouldBe(2);
        body.Overdue.ShouldBe(1);
        body.NoDueDate.ShouldBe(1);
        body.ByPriority.Count.ShouldBe(Enum.GetValues<Priority>().Length);   // luôn zero-fill đủ
    }

    [Fact]
    public async Task GetBacklogInsight_horizon_khong_duong_tra_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await pm.Client.GetAsync(
            $"/api/v1/projects/{projectId}/reports/backlog-insight?dueSoonHorizonDays=0");

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBacklogInsight_nguoi_ngoai_project_nhan_404_khong_phai_403()
    {
        var outsider = await CreateUserAsync();
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await outsider.Client.GetAsync(
            $"/api/v1/projects/{projectId}/reports/backlog-insight");

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetVelocity_chua_dong_sprint_nao_tra_danh_sach_rong()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await CreateSprintAsync(pm.Client, projectId);   // vẫn Planned, chưa Start/Complete

        var res = await pm.Client.GetAsync($"/api/v1/projects/{projectId}/reports/velocity");
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<VelocityResponse>(TestJson.Options);
        body!.Sprints.ShouldBeEmpty();
        body.AverageVelocity.ShouldBe(0);
    }

    [Fact]
    public async Task GetVelocity_sau_khi_dong_sprint_hien_dung_DoneCount_va_CompletedAt()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var sprintId = await CreateSprintAsync(pm.Client, projectId);

        var doneTask = await CreateTaskAsync(pm.Client, projectId, "Xong", sprintId: sprintId);
        await CreateTaskAsync(pm.Client, projectId, "Chưa xong", sprintId: sprintId);
        await MoveToColumnAsync(pm.Client, doneTask, targetOrder: 3);

        (await pm.Client.PostAsync($"/api/v1/sprints/{sprintId}/start", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await pm.Client.PostAsJsonAsync(
            $"/api/v1/sprints/{sprintId}/complete", new CompleteSprintRequest(null)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var res = await pm.Client.GetAsync($"/api/v1/projects/{projectId}/reports/velocity");
        var body = await res.Content.ReadFromJsonAsync<VelocityResponse>(TestJson.Options);

        var point = body!.Sprints.ShouldHaveSingleItem();
        point.SprintId.ShouldBe(sprintId);
        point.DoneCount.ShouldBe(1);
        // Task chưa xong đã rời sprint (đẩy về Backlog) lúc đóng sổ — chỉ còn 1 trong tổng.
        point.TotalCount.ShouldBe(1);
        body.AverageVelocity.ShouldBe(1);
    }

    [Fact]
    public async Task GetTimeline_liet_ke_ca_ba_vong_doi_va_sap_theo_StartDate()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        // Cố ý tạo THEO THỨ TỰ NGƯỢC với StartDate để bài test không "tình cờ" đúng nhờ
        // thứ tự tạo — chỉ đúng nếu server thật sự ORDER BY StartDate.
        var completedId = await CreateSprintAsync(pm.Client, projectId, "Đã đóng", startOffset: -21, endOffset: -7);
        var plannedId = await CreateSprintAsync(pm.Client, projectId, "Chưa bắt đầu", startOffset: 7, endOffset: 21);
        var activeId = await CreateSprintAsync(pm.Client, projectId, "Đang chạy", startOffset: -3, endOffset: 10);

        var doneTask = await CreateTaskAsync(pm.Client, projectId, "Xong", sprintId: completedId);
        await MoveToColumnAsync(pm.Client, doneTask, targetOrder: 3);

        (await pm.Client.PostAsync($"/api/v1/sprints/{completedId}/start", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await pm.Client.PostAsJsonAsync(
            $"/api/v1/sprints/{completedId}/complete", new CompleteSprintRequest(null)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await pm.Client.PostAsync($"/api/v1/sprints/{activeId}/start", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var res = await pm.Client.GetAsync($"/api/v1/projects/{projectId}/reports/timeline");
        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<TimelineResponse>(TestJson.Options);
        body!.Sprints.Count.ShouldBe(3);

        // Sắp theo StartDate: Đã đóng (-21) → Đang chạy (-3) → Chưa bắt đầu (+7).
        body.Sprints[0].SprintId.ShouldBe(completedId);
        body.Sprints[1].SprintId.ShouldBe(activeId);
        body.Sprints[2].SprintId.ShouldBe(plannedId);

        body.Sprints[0].Status.ShouldBe(SprintStatus.Completed);
        body.Sprints[0].Done.ShouldBe(1);
        body.Sprints[0].CompletedAt.ShouldNotBeNull();

        body.Sprints[1].Status.ShouldBe(SprintStatus.Active);
        body.Sprints[1].CompletedAt.ShouldBeNull();
        body.Sprints[1].IsOverdue.ShouldBeFalse();   // vẫn còn trong khoảng StartDate..EndDate

        body.Sprints[2].Status.ShouldBe(SprintStatus.Planned);
        body.Sprints[2].Total.ShouldBe(0);
        body.Sprints[2].IsOverdue.ShouldBeFalse();   // Planned không bao giờ "quá hạn"
    }

    [Fact]
    public async Task GetTimeline_sprint_Active_da_qua_EndDate_thi_IsOverdue_true()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var sprintId = await CreateSprintAsync(pm.Client, projectId, "Quá hạn", startOffset: -10, endOffset: -1);
        (await pm.Client.PostAsync($"/api/v1/sprints/{sprintId}/start", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var res = await pm.Client.GetAsync($"/api/v1/projects/{projectId}/reports/timeline");
        var body = await res.Content.ReadFromJsonAsync<TimelineResponse>(TestJson.Options);

        body!.Sprints.ShouldHaveSingleItem().IsOverdue.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTimeline_nguoi_ngoai_project_nhan_404()
    {
        var outsider = await CreateUserAsync();
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await outsider.Client.GetAsync($"/api/v1/projects/{projectId}/reports/timeline");

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
