using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Common.Services;

public class ActivityLogger : IActivityLogger
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public ActivityLogger(IUnitOfWork uow, ICurrentUserService currentUser)
        => (_uow, _currentUser) = (uow, currentUser);

    public void Log(string entityType, Guid entityId, ActivityAction action, string detail)
        => _uow.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Detail = detail,
            EmployeeId = _currentUser.RequireEmployeeId()
        });
}