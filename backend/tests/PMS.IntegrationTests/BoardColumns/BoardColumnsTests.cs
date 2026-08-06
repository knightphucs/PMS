using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PMS.Application.Features.BoardColumns;
using PMS.Application.Features.Statistics;
using PMS.Domain.Enums;
using PMS.IntegrationTests.Infrastructure;
using Shouldly;

namespace PMS.IntegrationTests.BoardColumns;

/// <summary>
/// Cột board tuỳ biến (ADR-052) — nợ kiểm chứng ghi ở `frontend-next-session.md`
/// ("đổi thứ tự cột chưa bấm thử trên UI", "đổi category cột có task chưa thử trên UI").
///
/// 🔴 Trước bộ test này, `BoardColumnsController`/`BoardColumnService` KHÔNG có lấy một
/// test nào — unit lẫn integration — dù tài liệu ghi "endpoint `PUT /columns/order` đã có
/// test". Đúng lớp lỗi mà dự án đã đặt tên nhiều lần: tài liệu ✅ mà thứ cần kiểm chứng
/// chưa có ai gọi tới. Hai test dưới đây thay cho phần "kéo–thả thật trên UI" mà môi
/// trường này không bắn được sự kiện con trỏ tổng hợp: chúng xác nhận đúng NHỮNG GÌ nút
/// bấm trên UI sẽ gọi tới — round-trip HTTP thật, không phải state lạc quan phía client.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class BoardColumnsTests : IntegrationTestBase
{
    public BoardColumnsTests(PmsWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Doi_thu_tu_cot_luu_that_va_giu_nguyen_sau_khi_GET_lai_tu_dau()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        var before = await GetColumnsAsync(pm.Client, projectId);
        before.Count.ShouldBe(4);   // bốn cột mặc định của ProjectService

        // Đảo ngược hoàn toàn thứ tự — đúng thao tác mà bấm "→" nhiều lần trên UI tạo ra.
        var reversedIds = before.OrderByDescending(c => c.Order).Select(c => c.Id).ToList();

        var reorder = await pm.Client.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}/columns/order",
            new ReorderBoardColumnsRequest(reversedIds));
        reorder.StatusCode.ShouldBe(HttpStatusCode.OK);

        // "Tải lại toàn trang" = một GET hoàn toàn mới, không đọc lại response của PUT ở
        // trên — đây là bằng chứng đã ghi xuống DB chứ không phải chỉ trả về đúng thứ vừa
        // gửi lên.
        var after = await GetColumnsAsync(pm.Client, projectId);
        after.OrderBy(c => c.Order).Select(c => c.Id).ShouldBe(reversedIds);
    }

    [Fact]
    public async Task Doi_thu_tu_cot_thieu_mot_cot_bi_tu_choi_400()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);
        var columns = await GetColumnsAsync(pm.Client, projectId);

        var incomplete = columns.Take(columns.Count - 1).Select(c => c.Id).ToList();

        var res = await pm.Client.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}/columns/order",
            new ReorderBoardColumnsRequest(incomplete));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Doi_category_cot_dang_co_task_dong_bo_lai_Category_cua_TUNG_task_va_phan_anh_o_thong_ke()
    {
        var pm = await CreateUserAsync();
        var projectId = await CreateProjectAsync(pm.Client);

        // Task mới luôn rơi vào cột mặc định (Order 0, "Cần làm", category ToDo).
        var taskId = await CreateTaskAsync(pm.Client, projectId, "Task cần đổi nhóm");
        var todoColumnId = await ColumnIdAsync(projectId, 0);

        var before = await pm.Client.GetFromJsonAsync<ProjectStatisticsResponse>(
            $"/api/v1/projects/{projectId}/statistics", TestJson.Options);
        before!.CompletionRate.ShouldBe(0);

        var columns = await GetColumnsAsync(pm.Client, projectId);
        var todoColumn = columns.Single(c => c.Id == todoColumnId);

        // Đổi NHÓM của cột (giữ tên/màu) từ ToDo sang Done — đúng thao tác "sửa cột, đổi
        // category" trên dialog Quản lý cột.
        var update = await pm.Client.PutAsJsonAsync(
            $"/api/v1/columns/{todoColumnId}",
            new UpdateBoardColumnRequest(todoColumn.Name, todoColumn.Color, StatusCategory.Done));
        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 1) Bản sao TaskItem.Category phải đồng bộ THẬT trong DB — không chỉ cột đổi tên nhóm.
        var taskCategory = await WithDbAsync(db => db.Tasks
            .Where(t => t.Id == taskId).Select(t => t.Category).SingleAsync());
        taskCategory.ShouldBe(StatusCategory.Done);

        // 2) Thống kê phải đọc lại đúng số MỚI — đây là phép kiểm mà tài liệu ghi "chưa xác
        // nhận bằng mắt trên UI"; ở đây xác nhận qua đúng endpoint mà tab Thống kê gọi.
        var after = await pm.Client.GetFromJsonAsync<ProjectStatisticsResponse>(
            $"/api/v1/projects/{projectId}/statistics", TestJson.Options);
        after!.CompletionRate.ShouldBe(100);
        after.ByStatus.Single(s => s.ColumnId == todoColumnId).Category.ShouldBe(StatusCategory.Done);
    }

    private static async Task<List<BoardColumnResponse>> GetColumnsAsync(HttpClient client, Guid projectId)
        => (await client.GetFromJsonAsync<List<BoardColumnResponse>>(
            $"/api/v1/projects/{projectId}/columns", TestJson.Options))!;
}
