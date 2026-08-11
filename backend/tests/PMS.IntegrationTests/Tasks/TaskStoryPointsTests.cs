using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Features.Reports;
using PMS.Application.Features.Sprints;
using PMS.Application.Features.Tasks;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.Tasks;

/// <summary>
/// Story Point (migration <c>AddStoryPointsToTasks</c>, 2026-08-07).
///
/// 🔴 <b>Vì sao bộ test này ra đời muộn hơn tính năng bốn ngày.</b> Story Point được thêm
/// trong một commit tự khai là "wip … chưa hoàn tất" và **không có một test nào chạm tới** —
/// từ entity, validator, đường ghi, cho tới cột <c>DoneStoryPoints</c> trong
/// <c>vw_SprintVelocity</c>. Đó đúng bằng lớp lỗi đã làm <c>StatisticsService</c> trả 500
/// suốt một ngày (ADR-046): <i>thứ cần kiểm chứng chưa có ai gọi tới</i>. Rà lại 2026-08-11
/// thì mã nguồn hóa ra ĐÚNG và đầy đủ — nhưng "đúng mà không ai kiểm" và "sai" trông giống
/// hệt nhau từ bên ngoài, nên khoản nợ vẫn phải trả.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class TaskStoryPointsTests : IntegrationTestBase
{
    public TaskStoryPointsTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Tao_task_kem_StoryPoints_thi_doc_lai_dung_o_ca_hai_DTO()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var taskId = await CreateTaskAsync(pm.Client, projectId, "Có điểm", storyPoints: 8);

        // TaskDetailResponse
        var detail = await pm.Client.GetFromJsonAsync<TaskDetailResponse>(
            $"/api/v1/tasks/{taskId}", TestJson.Options);
        detail!.StoryPoints.ShouldBe(8);

        // TaskSummaryResponse — hai mapper KHÁC nhau, nên phải kiểm cả hai. Bỏ sót một cái
        // là đúng hình dạng lỗi RMG020 sinh ra để bắt (nhưng analyzer đó đang tắt ở 11 mapper).
        var backlog = await pm.Client.GetFromJsonAsync<List<TaskSummaryResponse>>(
            $"/api/v1/projects/{projectId}/backlog", TestJson.Options);
        backlog!.ShouldHaveSingleItem().StoryPoints.ShouldBe(8);
    }

    [Fact]
    public async Task Khong_truyen_StoryPoints_thi_mac_dinh_0()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var detail = await pm.Client.GetFromJsonAsync<TaskDetailResponse>(
            $"/api/v1/tasks/{taskId}", TestJson.Options);

        detail!.StoryPoints.ShouldBe(0);
    }

    [Theory]
    [InlineData(-1)]      // âm
    [InlineData(1001)]    // vượt trần
    public async Task StoryPoints_ngoai_khoang_0_1000_thi_400(int storyPoints)
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var res = await pm.Client.PostAsJsonAsync("/api/v1/tasks",
            new CreateTaskRequest("Điểm sai", projectId, null, null, null, Priority.Medium,
                StoryPoints: storyPoints));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var count = await WithDbAsync(db => db.Tasks.CountAsync(t => t.ProjectId == projectId));
        count.ShouldBe(0);
    }

    [Fact]
    public async Task Sua_task_doi_duoc_StoryPoints_va_khong_lam_mat_truong_khac()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId, "Ban đầu", storyPoints: 3);

        var before = await pm.Client.GetFromJsonAsync<TaskDetailResponse>(
            $"/api/v1/tasks/{taskId}", TestJson.Options);

        var res = await pm.Client.PutAsJsonAsync($"/api/v1/tasks/{taskId}",
            new UpdateTaskRequest("Ban đầu", null, Priority.Medium, before!.RowVersion,
                Description: "Mô tả giữ nguyên", StoryPoints: 13));

        res.StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await pm.Client.GetFromJsonAsync<TaskDetailResponse>(
            $"/api/v1/tasks/{taskId}", TestJson.Options);
        after!.StoryPoints.ShouldBe(13);
        after.Description.ShouldBe("Mô tả giữ nguyên");
    }

    /// <summary>
    /// Phép kiểm ĐÁNG GIÁ NHẤT của file này: <c>DoneStoryPoints</c> không do C# tính mà do
    /// một biểu thức <c>CASE WHEN t.Category = 2</c> bên trong <c>vw_SprintVelocity</c>
    /// (ADR-055). Sai hằng số category ở đó thì không có compiler nào kêu, không có test nào
    /// khác đi qua, và báo cáo velocity chỉ đơn giản hiện một con số sai một cách hợp lý.
    /// </summary>
    [Fact]
    public async Task View_velocity_chi_cong_diem_cua_task_thuoc_nhom_Done()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var sprintId = await CreateSprintAsync(pm.Client, projectId);

        var done = await CreateTaskAsync(pm.Client, projectId, "Xong", sprintId: sprintId, storyPoints: 5);
        var alsoDone = await CreateTaskAsync(pm.Client, projectId, "Cũng xong", sprintId: sprintId, storyPoints: 2);
        // Task này có điểm nhưng KHÔNG xong -> điểm của nó phải nằm ngoài tổng.
        await CreateTaskAsync(pm.Client, projectId, "Dở dang", sprintId: sprintId, storyPoints: 100);

        await MoveToColumnAsync(pm.Client, done, targetOrder: 3);
        await MoveToColumnAsync(pm.Client, alsoDone, targetOrder: 3);

        (await pm.Client.PostAsync($"/api/v1/sprints/{sprintId}/start", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await pm.Client.PostAsJsonAsync(
            $"/api/v1/sprints/{sprintId}/complete", new CompleteSprintRequest(null)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await pm.Client.GetFromJsonAsync<VelocityResponse>(
            $"/api/v1/projects/{projectId}/reports/velocity", TestJson.Options);

        var point = body!.Sprints.ShouldHaveSingleItem();
        point.DoneStoryPoints.ShouldBe(7);          // 5 + 2, KHÔNG có 100
        body.AverageStoryPoints.ShouldBe(7m);
    }

    [Fact]
    public async Task View_velocity_sprint_khong_co_diem_nao_tra_0_chu_khong_phai_null()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var sprintId = await CreateSprintAsync(pm.Client, projectId);

        var done = await CreateTaskAsync(pm.Client, projectId, "Xong, 0 điểm", sprintId: sprintId);
        await MoveToColumnAsync(pm.Client, done, targetOrder: 3);

        (await pm.Client.PostAsync($"/api/v1/sprints/{sprintId}/start", null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await pm.Client.PostAsJsonAsync(
            $"/api/v1/sprints/{sprintId}/complete", new CompleteSprintRequest(null)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await pm.Client.GetFromJsonAsync<VelocityResponse>(
            $"/api/v1/projects/{projectId}/reports/velocity", TestJson.Options);

        // COALESCE trong view lo chuyện này — SUM() trên tập rỗng trả NULL, mà DoneStoryPoints
        // là int không nullable nên map về sẽ ném chứ không lặng lẽ ra 0.
        body!.Sprints.ShouldHaveSingleItem().DoneStoryPoints.ShouldBe(0);
    }
}
