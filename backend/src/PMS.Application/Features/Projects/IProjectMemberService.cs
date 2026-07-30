namespace PMS.Application.Features.Projects;

public interface IProjectMemberService
{
    Task<IReadOnlyList<ProjectMemberResponse>> GetMembersAsync(
        Guid projectId, CancellationToken ct = default);
    Task<ProjectMemberResponse> InviteAsync(
        Guid projectId, InviteMemberRequest request, CancellationToken ct = default);
    Task<ProjectMemberResponse> AcceptInvitationAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectMemberResponse> DeclineInvitationAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectMemberResponse> ChangeRoleAsync(
        Guid projectId, Guid employeeId, ChangeMemberRoleRequest request, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid projectId, Guid employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<MyInvitationResponse>> GetMyInvitationsAsync(CancellationToken ct = default);
}