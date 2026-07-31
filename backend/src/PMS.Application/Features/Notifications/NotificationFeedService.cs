using Microsoft.Extensions.Logging;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Domain.Entities;

namespace PMS.Application.Features.Notifications;

/// <summary>
/// Cố ý KHÔNG inject <c>IProjectAuthorizationService</c> và KHÔNG inject
/// <c>IActivityLogger</c> — cả hai đều là chủ ý, không phải bỏ sót:
/// <list type="bullet">
/// <item>Không authz project-scoped: thông báo là dữ liệu riêng của người nhận, không thuộc
/// project nào (ADR-023). Nhét vào khuôn <c>ProjectAction</c> sẽ phải bịa ra một projectId
/// cho những thông báo không liên quan tới project nào.</item>
/// <item>Không ActivityLog: ADR-013 sinh ra để ghi lại thay đổi nghiệp vụ trên
/// Project/Task cho audit trail. "Tôi đã xem thông báo của tôi" không phải thay đổi nghiệp
/// vụ, và ghi log mỗi lần mở chuông sẽ làm loãng đúng cái audit trail đó.</item>
/// </list>
/// </summary>
public class NotificationFeedService : INotificationFeedService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly NotificationMapper _mapper;
    private readonly ILogger<NotificationFeedService> _logger;

    public NotificationFeedService(
        IUnitOfWork uow, ICurrentUserService currentUser,
        NotificationMapper mapper, ILogger<NotificationFeedService> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PagedResult<NotificationResponse>> GetMineAsync(
        bool? isRead, PagedRequest request, CancellationToken ct = default)
    {
        var paged = await _uow.Notifications.GetPagedForRecipientAsync(
            _currentUser.RequireEmployeeId(), isRead, request, ct);

        return paged.Map(_mapper.ToResponse);
    }

    public async Task<UnreadCountResponse> GetUnreadCountAsync(CancellationToken ct = default)
        => new(await _uow.Notifications.CountUnreadAsync(_currentUser.RequireEmployeeId(), ct));

    public async Task<NotificationResponse> MarkAsReadAsync(
        Guid id, CancellationToken ct = default)
    {
        var employeeId = _currentUser.RequireEmployeeId();

        // Lọc theo người nhận nằm trong chính câu query: thông báo không tồn tại và thông báo
        // của người khác trả về cùng một 404, không để lộ sự tồn tại của bản ghi (ADR-023).
        var notification = await _uow.Notifications.GetForRecipientAsync(id, employeeId, ct)
            ?? throw new NotFoundException(nameof(Notification), id);

        // Đã đọc rồi thì không có gì để lưu — nhưng vẫn trả 200 kèm trạng thái hiện tại.
        if (notification.MarkAsRead())
            await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Đánh dấu đã đọc thông báo {NotificationId} của {EmployeeId}",
            id, employeeId);

        return _mapper.ToResponse(notification);
    }

    public async Task<MarkAllReadResponse> MarkAllAsReadAsync(CancellationToken ct = default)
    {
        var employeeId = _currentUser.RequireEmployeeId();

        // ADR-024: nạp qua ChangeTracker rồi sửa, KHÔNG dùng ExecuteUpdateAsync. Bulk update
        // đi thẳng xuống SQL nên bỏ qua ApplyAuditFields() -> bản ghi mất UpdatedAt, đúng lý
        // do ADR-008 đã chọn Option A cho soft delete. Số thông báo CHƯA ĐỌC của một người
        // luôn nhỏ (đọc xong là hết), nên nạp vào bộ nhớ không phải rủi ro thực tế.
        var unread = await _uow.Notifications.GetUnreadForRecipientAsync(employeeId, ct);

        foreach (var notification in unread) notification.MarkAsRead();

        // Một nghiệp vụ, một lần SaveChanges (ADR-007) — kể cả khi sửa nhiều bản ghi.
        if (unread.Count > 0) await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Đánh dấu đã đọc toàn bộ {Count} thông báo của {EmployeeId}",
            unread.Count, employeeId);

        return new MarkAllReadResponse(unread.Count);
    }
}
