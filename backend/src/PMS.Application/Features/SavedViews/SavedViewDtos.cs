using PMS.Application.Features.CustomFields;
using PMS.Application.Features.Tasks;
using PMS.Domain.Enums;

namespace PMS.Application.Features.SavedViews;

// ---------- khai báo view ----------

/// <summary>
/// Một dòng điều kiện. <paramref name="Field"/> và <paramref name="FieldDefinitionId"/> loại
/// trừ lẫn nhau — đúng một trong hai.
/// </summary>
public record SavedViewFilterDto(
    TaskField? Field,
    Guid? FieldDefinitionId,
    FilterOperator Operator,
    string? Value);

/// <summary>Một cột hiển thị. Cùng luật "đúng một nguồn trường" như bộ lọc.</summary>
public record SavedViewColumnDto(TaskField? Field, Guid? FieldDefinitionId);

public record SavedViewResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    Guid OwnerId,
    string OwnerName,
    bool IsShared,
    int Order,
    TaskField? SortBy,
    bool SortDescending,
    TaskField? GroupBy,
    IReadOnlyList<SavedViewFilterDto> Filters,
    IReadOnlyList<SavedViewColumnDto> Columns,
    /// <summary>
    /// Người đang gọi có sửa/xoá được view này không. Backend trả lời thay vì để frontend tự
    /// suy: luật là "chủ sở hữu HOẶC người có ManageSavedViews", mà vế thứ hai frontend chỉ
    /// biết qua vai trò trong project — hai nơi cùng dựng một luật thì chắc chắn có lúc lệch
    /// (ADR-034).
    /// </summary>
    bool CanEdit);

public record CreateSavedViewRequest(
    string Name,
    bool IsShared,
    TaskField? SortBy,
    bool SortDescending,
    TaskField? GroupBy,
    IReadOnlyList<SavedViewFilterDto>? Filters = null,
    IReadOnlyList<SavedViewColumnDto>? Columns = null);

/// <summary>
/// Ghi đè TOÀN PHẦN, giống <c>PUT /tasks/{id}</c> (ADR-044): bộ lọc và tập cột gửi lên thay
/// thế hẳn cái cũ. Trộn từng phần cho một cấu trúc dạng danh sách sẽ cần một ngữ nghĩa
/// "phần tử nào tương ứng phần tử nào" mà không có khoá tự nhiên nào đỡ.
/// </summary>
public record UpdateSavedViewRequest(
    string Name,
    bool IsShared,
    TaskField? SortBy,
    bool SortDescending,
    TaskField? GroupBy,
    IReadOnlyList<SavedViewFilterDto>? Filters = null,
    IReadOnlyList<SavedViewColumnDto>? Columns = null);

public record ReorderSavedViewsRequest(IReadOnlyList<Guid> ViewIds);

// ---------- chạy view ----------

/// <summary>
/// Đầu vào của một lượt chạy. KHÔNG nhận <c>viewId</c>: frontend giữ trạng thái view đang
/// mở (đã tải từ một <see cref="SavedViewResponse"/> hoặc đang sửa dở) rồi gửi lên.
///
/// <para>
/// 🔑 Nhờ vậy chỉ có MỘT đường chạy truy vấn, và "xem thử trước khi lưu" chạy được miễn phí.
/// Có thêm một endpoint <c>/views/{id}/tasks</c> riêng sẽ là đường thứ hai làm cùng một
/// việc — hai nơi cùng dựng một thứ thì có lúc lệch.
/// </para>
/// </summary>
public record TaskQueryRequest(
    IReadOnlyList<SavedViewFilterDto>? Filters = null,
    TaskField? SortBy = null,
    bool SortDescending = false,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>Giá trị trường tuỳ biến của một task, dạng gọn cho màn danh sách.</summary>
public record TaskListFieldValue(
    Guid FieldDefinitionId,
    string Label,
    FieldType Type,
    string? ValueText,
    decimal? ValueNumber,
    DateTime? ValueDate,
    bool? ValueBoolean,
    IReadOnlyList<FieldOptionResponse> SelectedOptions);

/// <summary>
/// Một dòng trên màn danh sách.
///
/// <para>
/// ⚠️ Cố ý KHÔNG nhồi <see cref="CustomFields"/> vào <see cref="TaskSummaryResponse"/>: DTO
/// đó nuôi cả board lẫn backlog, và thêm một collection vào nó sẽ buộc <b>mọi</b> query của
/// hai màn đó phải nhớ <c>Include(FieldValues)</c> — thiếu một chỗ là mảng rỗng một cách im
/// lặng, đúng lớp lỗi <c>SubtaskProgress</c>-luôn-0 (ADR-034).
/// </para>
/// </summary>
public record TaskListItemResponse(
    TaskSummaryResponse Task,
    IReadOnlyList<TaskListFieldValue> CustomFields);
