namespace PMS.Domain.Enums;

/// <summary>
/// Trường <b>dựng sẵn</b> của task mà một <see cref="Entities.SavedView"/> lọc / sắp xếp /
/// hiển thị được (ADR-061). Trường <b>tuỳ biến</b> đi đường riêng qua
/// <c>FieldDefinitionId</c> — xem <see cref="Entities.SavedViewFilter"/>.
///
/// <para>
/// 🔴 Danh mục ĐÓNG. Đây không phải "tên cột trong DB do client gửi lên" — nhận chuỗi tự do
/// rồi ghép vào truy vấn là mở đúng cánh cửa mà mọi tầng validation khác trong dự án đang
/// đóng. Mỗi thành viên ở đây có một nhánh dịch tường minh sang biểu thức LINQ.
/// </para>
/// <para>
/// 📌 <b>Cố ý chưa có</b> <c>Labels</c> và <c>Watchers</c>: cả hai là quan hệ N–N nên
/// "bằng" có hai nghĩa (chứa nhãn này / có đúng tập nhãn này), và chọn nhầm nghĩa thì người
/// dùng không có cách nào biết. Để tới khi có nhu cầu thật kèm một quyết định tường minh —
/// đúng nguyên tắc "không ship một cờ không chặn được gì" (§0).
/// </para>
/// </summary>
public enum TaskField
{
    /// <summary>Tên task — <see cref="FilterValueKind.Text"/>.</summary>
    Name,

    /// <summary>Cột board đang đứng — <see cref="FilterValueKind.Reference"/> (Id của cột).</summary>
    BoardColumn,

    /// <summary>
    /// Nhóm ngữ nghĩa của cột (<see cref="StatusCategory"/>) — <see cref="FilterValueKind.Enum"/>.
    /// Đây là thứ nên lọc khi muốn "mọi việc chưa xong" bất kể đội đặt tên cột là gì.
    /// </summary>
    Category,

    /// <summary>Độ ưu tiên — <see cref="FilterValueKind.Enum"/>.</summary>
    Priority,

    /// <summary>Loại công việc (ADR-060) — <see cref="FilterValueKind.Reference"/>.</summary>
    WorkItemType,

    /// <summary>Sprint; rỗng = đang ở Backlog — <see cref="FilterValueKind.Reference"/>.</summary>
    Sprint,

    /// <summary>Người được giao; rỗng = chưa ai nhận — <see cref="FilterValueKind.Reference"/>.</summary>
    Assignee,

    /// <summary>Người tạo/báo cáo — <see cref="FilterValueKind.Reference"/>.</summary>
    Reporter,

    /// <summary>Hạn hoàn thành — <see cref="FilterValueKind.Date"/>.</summary>
    DueDate,

    /// <summary>Story Points — <see cref="FilterValueKind.Number"/>.</summary>
    StoryPoints,

    /// <summary>Ngày tạo — <see cref="FilterValueKind.Date"/>.</summary>
    CreatedAt,

    /// <summary>
    /// Trạng thái duyệt hiện thời của task (ADR-063) — <see cref="FilterValueKind.Enum"/>,
    /// giá trị là một <see cref="TaskApprovalState"/>.
    ///
    /// <para>
    /// 🔑 Đây là thứ trả nốt lời hứa của ADR-061 (<i>"hàng đợi của một quy trình chính là
    /// một view lưu được"</i>). Trước nó, phê duyệt là cơ chế duy nhất trong hệ thống mà
    /// người dùng <b>không lọc theo được</b> — approver chỉ tới được task qua chuông.
    /// </para>
    /// <para>
    /// ⚠️ Đây là trường dựng sẵn ĐẦU TIÊN <b>không phải một cột của bảng Tasks</b>: nó là
    /// phép chiếu của bảng <c>Approvals</c> (hàng có <c>ConsumedAt IS NULL</c>) xuống task.
    /// Hệ quả bắt buộc nhớ: nhánh dịch của nó ở <c>TaskRepository</c> là một
    /// <c>.Any(...)</c> lồng, và vì vậy <b>sắp xếp theo nó không có nghĩa</b> — nó rơi về
    /// nhánh mặc định của <c>ApplyOrder</c>, đúng như <c>Assignee</c> đã làm.
    /// </para>
    /// </summary>
    ApprovalState
}
