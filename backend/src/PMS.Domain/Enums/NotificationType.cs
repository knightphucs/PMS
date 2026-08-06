namespace PMS.Domain.Enums;

public enum NotificationType
{
    TaskAssigned,
    TaskUnassigned,
    DueSoon,
    CommentAdded,
    StatusChanged,
    InvitedToProject,
    InvitationAccepted,
    InvitationDeclined,
    RoleChanged,
    RemovedFromProject,
    MemberLeftProject,

    /// <summary>
    /// Dự án được đánh dấu hoàn thành hoặc mở lại (2026-08-04).
    /// <para>
    /// 🔴 KHÔNG tái dùng <see cref="StatusChanged"/>: <c>RelatedEntityKind</c> được SUY RA từ
    /// <c>Type</c> (ADR-025), và <c>StatusChanged</c> suy ra <c>Task</c>. Dùng lại nó cho một
    /// thay đổi cấp project sẽ khiến chuông thông báo điều hướng tới <c>/tasks/{projectId}</c>
    /// — một id task không tồn tại. Đây chính là loại lệch mà việc suy ra (thay vì lưu hai
    /// cột) sinh ra để chặn.
    /// </para>
    /// </summary>
    ProjectStatusChanged,

    /// <summary>Được nhắc tên (@mention) trong một bình luận (2026-08-04).</summary>
    Mentioned,

    /// <summary>
    /// Sprint được đóng sổ (ADR-050, 2026-08-05).
    /// <para>
    /// 🔴 Cùng lý do với <see cref="ProjectStatusChanged"/>: phải là loại RIÊNG chứ không
    /// mượn <see cref="StatusChanged"/>. <c>RelatedEntityKind</c> suy ra từ <c>Type</c>
    /// (ADR-025) và <c>StatusChanged</c> suy ra <c>Task</c>, nên tái dùng sẽ khiến chuông
    /// điều hướng tới <c>/tasks/{projectId}</c> — một id không tồn tại.
    /// </para>
    /// <para>
    /// Điều hướng về <c>Project</c> chứ không về Sprint: sprint không có trang riêng, đường
    /// vào của nó là tab Sprint của dự án.
    /// </para>
    /// </summary>
    SprintCompleted
}