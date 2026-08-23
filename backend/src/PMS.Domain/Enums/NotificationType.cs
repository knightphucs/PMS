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
    SprintCompleted,

    // ---------- Nhóm phê duyệt (ADR-062, 2026-08-23) ----------
    //
    // 🔴 Cả ba đã được thêm vào nhánh `Task` của Notification.RelatedEntityKind. Kind SUY RA
    // từ Type (ADR-025) chứ không lưu cột, nên thêm một giá trị ở đây mà quên nhánh switch
    // là chuông điều hướng tới /tasks/{id} với một id không phải task — đúng cái bẫy
    // ProjectStatusChanged đã nổ ở ADR-048. Cả ba đều trỏ tới TASK vì đó là nơi khối duyệt
    // sống, và cũng là nơi người nhận thông báo cần tới để hành động.

    /// <summary>Có yêu cầu duyệt mới cần bạn quyết định. Gửi cho approver của policy.</summary>
    ApprovalRequested,

    /// <summary>Yêu cầu duyệt đã đủ quorum. Gửi cho người đã gửi yêu cầu.</summary>
    ApprovalApproved,

    /// <summary>Yêu cầu duyệt bị từ chối. Gửi cho người đã gửi yêu cầu.</summary>
    ApprovalRejected
}