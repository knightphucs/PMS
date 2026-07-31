using PMS.Domain.Enums;

namespace PMS.Application.Features.Notifications;

/// <summary>
/// <paramref name="RelatedEntityKind"/> suy ra từ <paramref name="Type"/> ở tầng domain
/// (ADR-025) — frontend dùng cặp (Kind, Id) để điều hướng khi bấm vào thông báo, không phải
/// tự dựng bảng "loại nào trỏ tới đâu" rồi lệch dần khỏi backend.
/// <para>
/// Không có <c>EmployeeId</c> người nhận: mọi thông báo trả về đều của chính người gọi, đưa
/// thêm chỉ mời client hiểu sai rằng có thể đọc thông báo của người khác.
/// </para>
/// </summary>
public record NotificationResponse(
    Guid Id,
    NotificationType Type,
    string Content,
    bool IsRead,
    Guid? RelatedEntityId,
    RelatedEntityKind RelatedEntityKind,
    DateTime CreatedAt);

public record UnreadCountResponse(int UnreadCount);

/// <summary>
/// <paramref name="MarkedCount"/> là số bản ghi THẬT SỰ đổi trạng thái, nên gọi lần hai trả
/// về 0 chứ không phải lỗi — luồng đánh dấu đã đọc là idempotent (ADR-023).
/// </summary>
public record MarkAllReadResponse(int MarkedCount);
