using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Tasks;

public static class TaskAuthorizationExtensions
{
    public static async Task<RoleInProject> AuthorizeTaskAsync(
        this IProjectAuthorizationService authz,
        TaskItem task, ProjectAction action, CancellationToken ct = default)
    {
        try
        {
            return await authz.AuthorizeAsync(task.ProjectId, action, ct);
        }
        catch (NotFoundException)
        {
            throw new NotFoundException(nameof(TaskItem), task.Id);
        }
    }
}
