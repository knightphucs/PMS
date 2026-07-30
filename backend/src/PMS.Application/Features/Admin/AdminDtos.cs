using PMS.Domain.Enums;

namespace PMS.Application.Features.Admin;

public record LockAccountRequest(string Reason);
public record ChangeSystemRoleRequest(SystemRole Role);

public record EmployeeAdminResponse(
    Guid Id, string Name, string Email, SystemRole SystemRole,
    bool IsLocked, DateTime? LockedAt, string? LockReason, DateTime CreatedAt);