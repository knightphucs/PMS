using PMS.Domain.Enums;

namespace PMS.Application.Features.BoardColumns;

/// <summary>Cột board đầy đủ — dùng cho danh sách cột và màn quản lý cột (ADR-052).</summary>
public record BoardColumnResponse(
    Guid Id,
    string Name,
    string Color,
    int Order,
    StatusCategory Category,
    /// <summary>
    /// Số task đang đứng trong cột. Có mặt vì dialog xóa cột phải nói rõ
    /// <i>"12 task sẽ được chuyển đi đâu?"</i> — hỏi mà không cho biết bao nhiêu thì người
    /// dùng không có cơ sở nào để chọn.
    /// </summary>
    int TaskCount);

public record CreateBoardColumnRequest(string Name, string Color, StatusCategory Category);

/// <summary>
/// Sửa cột. <c>Category</c> đổi được, và đó là lý do <c>BoardColumnService</c> phải cập
/// nhật <c>TaskItem.Category</c> của mọi task trong cột ngay sau đó — bản sao đó là dữ liệu
/// trùng có chủ đích, xem chú thích ở <c>TaskItem.Category</c>.
/// </summary>
public record UpdateBoardColumnRequest(string Name, string Color, StatusCategory Category);

/// <summary>
/// Xóa cột. <c>TargetColumnId</c> <b>bắt buộc khi cột còn task</b>.
///
/// <para>
/// 🔴 Không cho phép xóa "cuốn theo" task, và cũng không tự chọn hộ cột đích. Task là dữ
/// liệu người dùng đã bỏ công tạo; một cú bấm đổi cấu hình board không được phép làm mất
/// chúng, mà cũng không được âm thầm dồn chúng vào một cột do máy chọn — người dùng sẽ
/// không biết chỗ nào mà tìm.
/// </para>
/// </summary>
public record DeleteBoardColumnRequest(Guid? TargetColumnId);

/// <summary>
/// Sắp xếp lại toàn bộ cột. Nhận <b>trọn danh sách theo thứ tự mới</b> chứ không nhận
/// "chuyển cột X tới vị trí n": gửi cả dải thì trạng thái cuối cùng là tường minh và
/// idempotent, còn thao tác tương đối thì hai người kéo cùng lúc sẽ ra kết quả phụ thuộc
/// thứ tự tới của request.
/// </summary>
public record ReorderBoardColumnsRequest(IReadOnlyList<Guid> OrderedColumnIds);
