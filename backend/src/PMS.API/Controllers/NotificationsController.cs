using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Models;
using PMS.Application.Features.Notifications;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationFeedService _notifications;

    public NotificationsController(INotificationFeedService notifications)
        => _notifications = notifications;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<NotificationResponse>>> GetMine(
        [FromQuery] bool? isRead, [FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _notifications.GetMineAsync(isRead, request, ct));

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken ct)
        => Ok(await _notifications.GetUnreadCountAsync(ct));

    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationResponse>> MarkAsRead(Guid id, CancellationToken ct)
        => Ok(await _notifications.MarkAsReadAsync(id, ct));

    [HttpPatch("read-all")]
    [ProducesResponseType(typeof(MarkAllReadResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MarkAllReadResponse>> MarkAllAsRead(CancellationToken ct)
        => Ok(await _notifications.MarkAllAsReadAsync(ct));
}
