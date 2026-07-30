using PMS.Domain.Enums;

namespace PMS.Application.Common.Interfaces;

public interface INotificationService
{
    void Notify(Guid recipientId, NotificationType type, string content, Guid? relatedEntityId = null);
    void NotifyMany(IEnumerable<Guid> recipientIds, NotificationType type,
        string content, Guid? relatedEntityId = null);
}