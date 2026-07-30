using Microsoft.Extensions.Logging;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Admin;

public class EmployeeAdminService : IEmployeeAdminService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly EmployeeAdminMapper _mapper;
    private readonly ILogger<EmployeeAdminService> _logger;

    public EmployeeAdminService(
        IUnitOfWork uow, ICurrentUserService currentUser, IActivityLogger activityLog,
        EmployeeAdminMapper mapper, ILogger<EmployeeAdminService> logger)
    {
        _uow = uow; _currentUser = currentUser; _activityLog = activityLog;
        _mapper = mapper; _logger = logger;
    }

    public async Task<PagedResult<EmployeeAdminResponse>> GetPagedAsync(
        PagedRequest request, CancellationToken ct = default)
    {
        // KHÔNG kiểm quyền ở đây — policy "RequireSystemAdmin" ở controller đã chặn.
        // Đây là quyền tầng 1 (SystemRole), không phải quyền theo tài nguyên như Project.
        var paged = await _uow.Employees.GetPagedAsync(request, ct);
        return paged.Map(_mapper.ToAdminResponse);
    }

    public async Task LockAsync(Guid employeeId, LockAccountRequest request, CancellationToken ct = default)
    {
        var actorId = _currentUser.RequireEmployeeId();

        if (employeeId == actorId)
            throw new BusinessRuleException("Không thể tự khóa tài khoản của chính mình.");

        var employee = await RequireEmployeeAsync(employeeId, ct);

        if (employee.SystemRole == SystemRole.SystemAdmin)
            await EnsureAnotherActiveAdminExistsAsync(employeeId, ct);

        employee.Lock(request.Reason);   // DomainException -> 409 nếu đã khóa

        // Mấu chốt: khóa mà không thu hồi refresh token thì người bị khóa vẫn
        // lấy được access token mới suốt 7 ngày.
        foreach (var token in await _uow.RefreshTokens.GetActiveByEmployeeAsync(employeeId, ct))
            token.Revoke();

        _activityLog.Log(nameof(Employee), employeeId, ActivityAction.AccountLocked,
            $"Khóa tài khoản {employee.Email}. Lý do: {employee.LockReason}");

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning("Khóa tài khoản {EmployeeId} bởi admin {ActorId}. Lý do: {Reason}",
            employeeId, actorId, employee.LockReason);
    }

    public async Task UnlockAsync(Guid employeeId, CancellationToken ct = default)
    {
        var employee = await RequireEmployeeAsync(employeeId, ct);

        employee.Unlock();   // DomainException -> 409 nếu đang không bị khóa

        _activityLog.Log(nameof(Employee), employeeId, ActivityAction.AccountUnlocked,
            $"Mở khóa tài khoản {employee.Email}");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Mở khóa tài khoản {EmployeeId} bởi admin {ActorId}",
            employeeId, _currentUser.EmployeeId);
    }

    public async Task ChangeSystemRoleAsync(
        Guid employeeId, ChangeSystemRoleRequest request, CancellationToken ct = default)
    {
        var actorId = _currentUser.RequireEmployeeId();

        if (employeeId == actorId)
            throw new BusinessRuleException(
                "Không thể tự đổi vai trò hệ thống của chính mình. " +
                "Hãy nhờ một SystemAdmin khác thực hiện.");

        var employee = await RequireEmployeeAsync(employeeId, ct);
        var oldRole = employee.SystemRole;

        if (oldRole == request.Role) return;

        // Bất biến song song với "project luôn còn ≥1 PM": hệ thống luôn còn ≥1 admin
        // đang hoạt động (không bị khóa), nếu không sẽ không ai mở khóa được cho ai.
        if (oldRole == SystemRole.SystemAdmin)
            await EnsureAnotherActiveAdminExistsAsync(employeeId, ct);

        employee.ChangeSystemRole(request.Role);

        // SystemRole nằm trong JWT claim -> access token cũ vẫn mang role cũ.
        // Thu hồi refresh token để cửa sổ rủi ro chỉ còn bằng tuổi thọ access token (15 phút).
        foreach (var token in await _uow.RefreshTokens.GetActiveByEmployeeAsync(employeeId, ct))
            token.Revoke();

        _activityLog.Log(nameof(Employee), employeeId, ActivityAction.SystemRoleChanged,
            $"Đổi vai trò hệ thống của {employee.Email}: {oldRole} -> {request.Role}");

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning("Đổi SystemRole của {EmployeeId}: {Old} -> {New} bởi admin {ActorId}",
            employeeId, oldRole, request.Role, actorId);
    }

    // ---------- private ----------

    private async Task<Employee> RequireEmployeeAsync(Guid id, CancellationToken ct)
        => await _uow.Employees.GetByIdAsync(id, ct)
           ?? throw new NotFoundException(nameof(Employee), id);

    private async Task EnsureAnotherActiveAdminExistsAsync(Guid excludingId, CancellationToken ct)
    {
        var remaining = await _uow.Employees.CountActiveAdminsExceptAsync(excludingId, ct);

        if (remaining == 0)
            throw new ConflictException(
                "Đây là SystemAdmin đang hoạt động cuối cùng. " +
                "Hãy cấp quyền cho người khác trước khi khóa hoặc hạ vai trò tài khoản này.");
    }
}