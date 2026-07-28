using PMS.Domain.Enums;

namespace PMS.Application.Common.Interfaces;

public interface IActivityLogger
{
    void Log(string entityType, Guid entityId, ActivityAction action, string detail);
}