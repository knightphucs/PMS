namespace PMS.Domain.Enums;

/// <summary>
/// Loại entity mà <c>Notification.RelatedEntityId</c> đang trỏ tới. Suy ra từ
/// <see cref="NotificationType"/>, KHÔNG lưu thành cột (ADR-025) — xem
/// <c>Notification.RelatedEntityKind</c>.
/// </summary>
public enum RelatedEntityKind
{
    /// <summary>Không có entity liên quan (<c>RelatedEntityId</c> là null).</summary>
    None,
    Project,
    Task
}
