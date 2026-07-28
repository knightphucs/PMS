using PMS.Domain.Enums;

namespace PMS.Application.Features.Projects;

public record InviteMemberRequest(string Email, RoleInProject Role);

public record ChangeMemberRoleRequest(RoleInProject Role);

public record MyInvitationResponse(
    Guid ProjectId, string ProjectName, RoleInProject Role, DateTime InvitedAt);