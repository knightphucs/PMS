using PMS.Application.Common.Models;

namespace PMS.Application.Features.Notifications;

/// <summary>
/// Phía ĐỌC của thông báo (hộp thông báo / notification bell). Tên "Feed" để phân biệt rõ
/// với <c>Common.Services.INotificationService</c> — cái đó là phía GHI, một cross-cutting
/// concern cùng loại với <c>IActivityLogger</c> nên nằm ở <c>Common</c> và được mọi service
/// nghiệp vụ gọi tới; còn đây là một feature bình thường, chỉ controller của nó gọi.
/// <para>
/// Mọi method đều ngầm định "của chính người đang đăng nhập": id người nhận lấy từ
/// <c>ICurrentUserService</c>, không nhận từ tham số, nên không có đường nào để client đọc
/// thông báo của người khác (ADR-023).
/// </para>
/// </summary>
public interface INotificationFeedService
{
    Task<PagedResult<NotificationResponse>> GetMineAsync(
        bool? isRead, PagedRequest request, CancellationToken ct = default);

    Task<UnreadCountResponse> GetUnreadCountAsync(CancellationToken ct = default);

    Task<NotificationResponse> MarkAsReadAsync(Guid id, CancellationToken ct = default);

    Task<MarkAllReadResponse> MarkAllAsReadAsync(CancellationToken ct = default);
}
