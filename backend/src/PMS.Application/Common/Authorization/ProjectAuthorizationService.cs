// PMS.Application/Common/Authorization/ProjectAuthorizationService.cs
using Microsoft.Extensions.Logging;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Common.Authorization;

public class ProjectAuthorizationService : IProjectAuthorizationService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ProjectAuthorizationService> _logger;

    public ProjectAuthorizationService(
        IUnitOfWork uow, ICurrentUserService currentUser,
        ILogger<ProjectAuthorizationService> logger)
        => (_uow, _currentUser, _logger) = (uow, currentUser, logger);

    public async Task<RoleInProject> AuthorizeAsync(
        Guid projectId, ProjectAction action, CancellationToken ct = default)
    {
        var employeeId = _currentUser.RequireEmployeeId();

        var role = await _uow.Projects.GetRoleInProjectAsync(projectId, employeeId, ct);

        if (role is null)
        {
            _logger.LogInformation(
                "Từ chối {Action} trên project {ProjectId}: {EmployeeId} không phải thành viên",
                action, projectId, employeeId);
            throw new NotFoundException(nameof(Project), projectId);
        }

        if (!ProjectPermissions.IsAllowed(action, role.Value))
        {
            _logger.LogInformation(
                "Từ chối {Action} trên project {ProjectId}: {EmployeeId} chỉ có role {Role}",
                action, projectId, employeeId, role.Value);
            throw new ForbiddenException();
        }

        return role.Value;
    }
}