using PMS.Application.Common.Models;

namespace PMS.Application.Features.Admin;

public interface IEmployeeAdminService
{
    Task<PagedResult<EmployeeAdminResponse>> GetPagedAsync(
        PagedRequest request, CancellationToken ct = default);

    Task LockAsync(Guid employeeId, LockAccountRequest request, CancellationToken ct = default);

    Task UnlockAsync(Guid employeeId, CancellationToken ct = default);

    Task ChangeSystemRoleAsync(
        Guid employeeId, ChangeSystemRoleRequest request, CancellationToken ct = default);
}