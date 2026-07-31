using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

public class Notification : BaseEntity
{
    public NotificationType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public Guid? RelatedEntityId { get; set; }

    public Guid EmployeeId { get; set; }
    public Employee Recipient { get; set; } = null!;

    /// <summary>
    /// Đánh dấu đã đọc. Cố ý KHÔNG ném lỗi khi đã đọc từ trước (khác
    /// <c>ProjectMember.Accept()</c>): bấm chuông thông báo hai lần không phải vi phạm
    /// nghiệp vụ, và một luồng đọc idempotent thì client không cần dò trạng thái trước
    /// khi gọi. Trả về false nếu vốn đã đọc, để service phân biệt "không có gì thay đổi"
    /// khi cần đếm số bản ghi thật sự bị sửa (ADR-023).
    /// </summary>
    public bool MarkAsRead()
    {
        if (IsRead) return false;

        IsRead = true;
        return true;
    }

    /// <summary>
    /// <c>RelatedEntityId</c> chỉ là một Guid trơn: thông báo về project và về task đều nhét
    /// id vào đó, nên client không tự biết phải điều hướng tới đâu. Loại entity suy ra được
    /// từ <see cref="Type"/> nên KHÔNG thêm cột (ADR-025) — dữ liệu đã tồn tại không phải
    /// migrate, và không sinh ra khả năng Type với RelatedEntityKind lệch nhau.
    /// <para>
    /// Là property (không phải method) để Mapperly map tự động, và get-only không backing
    /// field nên EF Core bỏ qua — cùng khuôn <c>TaskItem.IsOverdue</c>/<c>SubtaskProgress</c>.
    /// </para>
    /// </summary>
    public RelatedEntityKind RelatedEntityKind => Type switch
    {
        NotificationType.InvitedToProject
        or NotificationType.InvitationAccepted
        or NotificationType.InvitationDeclined
        or NotificationType.RoleChanged
        or NotificationType.RemovedFromProject
        or NotificationType.MemberLeftProject  => RelatedEntityKind.Project,

        NotificationType.TaskAssigned
        or NotificationType.TaskUnassigned
        or NotificationType.DueSoon
        or NotificationType.CommentAdded
        or NotificationType.StatusChanged      => RelatedEntityKind.Task,

        _ => RelatedEntityKind.None
    };
}
