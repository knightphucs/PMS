using PMS.Domain.Enums;

namespace PMS.Application.Common.Authorization;

public interface IProjectAuthorizationService
{
    Task<RoleInProject> AuthorizeAsync(Guid projectId, ProjectAction action, CancellationToken ct = default);
}