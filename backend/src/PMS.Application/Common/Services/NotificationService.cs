using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Common.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public NotificationService(IUnitOfWork uow, ICurrentUserService currentUser)
        => (_uow, _currentUser) = (uow, currentUser);

    public void Notify(Guid recipientId, NotificationType type, string content,
        Guid? relatedEntityId = null)
        => _uow.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            EmployeeId = recipientId,
            Type = type,
            Content = content,
            RelatedEntityId = relatedEntityId,
            IsRead = false
        });

    public void NotifyMany(IEnumerable<Guid> recipientIds, NotificationType type,
        string content, Guid? relatedEntityId = null)
    {
        var actorId = _currentUser.EmployeeId;

        foreach (var id in recipientIds.Distinct().Where(id => id != actorId))
            Notify(id, type, content, relatedEntityId);
    }
}