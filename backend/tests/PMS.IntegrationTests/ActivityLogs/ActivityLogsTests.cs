using System.Net;
using System.Net.Http.Json;
using PMS.Application.Common.Models;
using PMS.Application.Features.ActivityLogs;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.ActivityLogs;

/// <summary>
/// Ba endpoint đọc nhật ký. Trước 2026-08-04 <c>ActivityLog</c> chỉ được kiểm như một TÁC
/// DỤNG PHỤ trong các test khác — không có test nào gọi thẳng vào đường đọc, và chính khoảng
/// trống đó để lọt việc <c>?search=</c> bị nuốt im lặng ở cả ba route.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ActivityLogsTests : IntegrationTestBase
{
    public ActivityLogsTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Lich_su_task_ghi_lai_thao_tac_va_doc_duoc()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var log = await pm.Client.GetFromJsonAsync<PagedResult<ActivityLogResponse>>(
            $"/api/v1/tasks/{taskId}/activity", TestJson.Options);

        log!.Items.ShouldContain(x => x.Action == ActivityAction.Created);
        log.Items.ShouldAllBe(x => x.ActorName.Length > 0);
    }

    /// <summary>
    /// 🔴 Lỗi từng lọt vì không có file này: <c>ActivityLogRepository</c> nhận
    /// <c>?search=</c> rồi <b>bỏ qua im lặng</b> — trả HTTP 200 kèm nguyên trang chưa lọc,
    /// tức một câu trả lời SAI mà client không có cách nào phát hiện.
    /// </summary>
    [Fact]
    public async Task Search_thuc_su_loc_chu_khong_bi_bo_qua_im_lang()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        // Sinh thêm một loại hoạt động khác để trang có ít nhất hai dòng khác nội dung.
        await MoveToColumnAsync(pm.Client, taskId, 1);

        var all = await pm.Client.GetFromJsonAsync<PagedResult<ActivityLogResponse>>(
            $"/api/v1/tasks/{taskId}/activity?pageSize=100", TestJson.Options);

        all!.TotalCount.ShouldBeGreaterThan(1);

        // Lấy một từ chỉ có trong MỘT dòng, rồi kiểm số lượng thật sự giảm.
        var keyword = all.Items.Single(x => x.Action == ActivityAction.StatusChanged).Detail;

        var filtered = await pm.Client.GetFromJsonAsync<PagedResult<ActivityLogResponse>>(
            $"/api/v1/tasks/{taskId}/activity?pageSize=100&search={Uri.EscapeDataString(keyword)}",
            TestJson.Options);

        filtered!.TotalCount.ShouldBeLessThan(all.TotalCount);
        filtered.Items.ShouldAllBe(x => x.Detail.Contains(keyword));

        // Từ khóa chắc chắn không tồn tại phải cho trang RỖNG. Không có khẳng định này thì
        // một bộ lọc "luôn khớp" vẫn làm khẳng định phía trên xanh.
        var none = await pm.Client.GetFromJsonAsync<PagedResult<ActivityLogResponse>>(
            $"/api/v1/tasks/{taskId}/activity?search=khong-ton-tai-{Guid.NewGuid():N}",
            TestJson.Options);

        none!.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Lich_su_project_gom_ca_hoat_dong_sprint()
    {
        // SprintService ghi log với EntityType = "Project" (không phải "Sprint"), nên feed
        // này gồm cả hoạt động sprint — đúng thiết kế, nhưng dễ bất ngờ nên khóa lại.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        await CreateSprintAsync(pm.Client, projectId);

        var log = await pm.Client.GetFromJsonAsync<PagedResult<ActivityLogResponse>>(
            $"/api/v1/projects/{projectId}/activity?pageSize=100", TestJson.Options);

        // Tạo project + tạo sprint = ít nhất hai dòng Created dưới cùng EntityType Project.
        log!.Items.Count(x => x.Action == ActivityAction.Created).ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Nguoi_ngoai_project_khong_doc_duoc_lich_su()
    {
        var pm = await CreateUserAsync();
        var outsider = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        (await outsider.Client.GetAsync($"/api/v1/projects/{projectId}/activity"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await outsider.Client.GetAsync($"/api/v1/tasks/{taskId}/activity"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Viewer_doc_duoc_lich_su()
    {
        // Đọc đi qua ProjectAction.View nên cả Viewer cũng thấy — đối trọng của test trên.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);

        var viewer = await CreateUserAsync();
        await InviteAndAcceptAsync(pm.Client, viewer, projectId, RoleInProject.Viewer);

        (await viewer.Client.GetAsync($"/api/v1/tasks/{taskId}/activity"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Phan_trang_on_dinh_khi_nhieu_dong_cung_moc_thoi_gian()
    {
        // Nhiều dòng log của cùng một SaveChanges có CreatedAt giống hệt nhau. Thiếu tie-break
        // theo Id thì thứ tự giữa hai lần gọi có thể khác, và phân trang trả trùng/sót dòng.
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var taskId = await CreateTaskAsync(pm.Client, projectId);
        await MoveToColumnAsync(pm.Client, taskId, 1);

        var first = await pm.Client.GetFromJsonAsync<PagedResult<ActivityLogResponse>>(
            $"/api/v1/tasks/{taskId}/activity?pageSize=100", TestJson.Options);
        var second = await pm.Client.GetFromJsonAsync<PagedResult<ActivityLogResponse>>(
            $"/api/v1/tasks/{taskId}/activity?pageSize=100", TestJson.Options);

        first!.Items.Select(x => x.Id).ShouldBe(second!.Items.Select(x => x.Id));
    }
}
