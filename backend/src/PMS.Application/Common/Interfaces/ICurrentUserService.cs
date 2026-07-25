using PMS.Domain.Enums;

namespace PMS.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? EmployeeId { get; }
    SystemRole? SystemRole { get; }
    string? IpAddress { get; }
    bool IsAuthenticated { get; }
}