using PMS.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.Notifications;

[Mapper]
public partial class NotificationMapper
{
    // RelatedEntityKind không cần MapProperty: nó là property computed trên entity nên
    // Mapperly khớp theo tên như mọi property khác (ADR-025).
#pragma warning disable RMG020 // Source member is not mapped to any target member
    public partial NotificationResponse ToResponse(Notification notification);
#pragma warning restore RMG020 // Source member is not mapped to any target member
}
