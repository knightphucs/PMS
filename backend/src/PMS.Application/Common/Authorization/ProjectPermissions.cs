using PMS.Domain.Enums;

namespace PMS.Application.Common.Authorization;

public static class ProjectPermissions
{
    public static bool IsAllowed(ProjectAction action, RoleInProject role) => action switch
    {
        ProjectAction.View             => true,
        ProjectAction.Update
        or ProjectAction.Delete
        or ProjectAction.ManageMembers => role is RoleInProject.ProjectManager,
        ProjectAction.ViewStatistics   => role is RoleInProject.ProjectManager
                                               or RoleInProject.Viewer,
        _ => false
    };
}