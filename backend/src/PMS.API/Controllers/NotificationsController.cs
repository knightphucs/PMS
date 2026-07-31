using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Models;
using PMS.Application.Features.Notifications;

namespace PMS.API.Controllers;

/// <summary>
/// Hộp thông báo của chính người đang đăng nhập. Không có route nào nhận employeeId: người
/// nhận luôn lấy từ JWT (ADR-023) — mở đường cho client tự khai người nhận là mở đường đọc
/// thông báo của người khác.
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationFeedService _notifications;

    public NotificationsController(INotificationFeedService notifications)
        => _notifications = notifications;

    /// <summary>
    /// Thông báo của tôi, mới nhất trước. Bỏ trống <paramref name="isRead"/> để lấy cả đã đọc
    /// và chưa đọc; <c>isRead=false</c> để lấy riêng phần chưa đọc.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<NotificationResponse>>> GetMine(
        [FromQuery] bool? isRead, [FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _notifications.GetMineAsync(isRead, request, ct));

    /// <summary>Số thông báo chưa đọc — endpoint riêng để badge trên chuông không phải tải cả danh sách.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken ct)
        => Ok(await _notifications.GetUnreadCountAsync(ct));

    /// <summary>
    /// Đánh dấu một thông báo đã đọc. Idempotent: gọi lại vẫn 200 (ADR-023).
    /// Thông báo của người khác trả 404, không phải 403.
    /// </summary>
    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationResponse>> MarkAsRead(Guid id, CancellationToken ct)
        => Ok(await _notifications.MarkAsReadAsync(id, ct));

    /// <summary>Đánh dấu tất cả đã đọc; trả về số bản ghi thật sự đổi trạng thái.</summary>
    [HttpPatch("read-all")]
    [ProducesResponseType(typeof(MarkAllReadResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MarkAllReadResponse>> MarkAllAsRead(CancellationToken ct)
        => Ok(await _notifications.MarkAllAsReadAsync(ct));
}
