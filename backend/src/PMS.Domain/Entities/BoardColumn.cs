using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// Một cột trên board của MỘT project — thay cho enum <c>Status</c> cũ của task (ADR-052).
///
/// <para>
/// 🔴 <b>Tên là <c>BoardColumn</c> chứ không phải <c>TaskStatus</c> có lý do cụ thể:</b>
/// <c>System.Threading.Tasks.TaskStatus</c> đã tồn tại trong BCL, và file nào lỡ có cả
/// <c>using System.Threading.Tasks;</c> lẫn <c>using PMS.Domain.Entities;</c> sẽ nhận lỗi
/// mơ hồ kiểu — trong một solution mà <c>Task&lt;T&gt;</c> xuất hiện ở mọi chữ ký async thì
/// đó là bẫy chắc chắn nổ. Tên này cũng đúng thứ người dùng nhìn thấy: một cột.
/// </para>
/// </summary>
public class BoardColumn : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>Tên do người dùng đặt — "Cần làm", "Chờ QA", "Đã ship"…</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mã màu <c>#RRGGBB</c> cho chip trạng thái.</summary>
    public string Color { get; set; } = "#6B7280";

    /// <summary>
    /// Vị trí trái→phải trên board. Không unique: đổi thứ tự là ghi lại cả dải nên một
    /// ràng buộc unique chỉ tổ làm bước trung gian của thao tác đó vi phạm.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Nhóm ngữ nghĩa. Xem <see cref="StatusCategory"/> — đây là thứ mọi phép kiểm
    /// "task xong chưa" trong solution đọc, thay cho việc so tên cột.
    /// </summary>
    public StatusCategory Category { get; set; } = StatusCategory.ToDo;

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

    /// <summary>
    /// Bốn cột mặc định cấp cho project mới — đúng bốn trạng thái mà hệ thống dùng trước
    /// ADR-052, nên project không đụng gì tới cấu hình cột vẫn hành xử y như cũ.
    /// </summary>
    /// <remarks>
    /// Dùng chung cho cả <c>ProjectService</c> (project mới) lẫn migration backfill (project
    /// đang có). Hai nơi tự định nghĩa danh sách này thì dữ liệu cũ và mới lệch nhau ngay
    /// từ ngày đầu.
    /// </remarks>
    public static IReadOnlyList<BoardColumn> CreateDefaults(Guid projectId) =>
    [
        New(projectId, "Cần làm", "#6B7280", 0, StatusCategory.ToDo),
        New(projectId, "Đang làm", "#3B82F6", 1, StatusCategory.InProgress),
        New(projectId, "Đang duyệt", "#A855F7", 2, StatusCategory.InProgress),
        New(projectId, "Hoàn thành", "#22C55E", 3, StatusCategory.Done),
    ];

    private static BoardColumn New(
        Guid projectId, string name, string color, int order, StatusCategory category) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = projectId,
        Name = name,
        Color = color,
        Order = order,
        Category = category,
    };
}
