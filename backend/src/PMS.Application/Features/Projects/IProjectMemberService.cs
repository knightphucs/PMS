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

    /// <summary>Mời một email vào project qua link gửi bằng email — hoạt động cả khi email chưa có tài khoản.</summary>
    Task<ExternalInvitationResponse> InviteExternalAsync(
        Guid projectId, InviteExternalRequest request, CancellationToken ct = default);

    /// <summary>Xem trước một lời mời từ token thô trong link — public, không cần đăng nhập.</summary>
    Task<InvitationPreviewResponse> GetInvitationPreviewAsync(
        string rawToken, CancellationToken ct = default);

    /// <summary>Chấp nhận lời mời qua email — người gọi phải đã đăng nhập bằng đúng email được mời.</summary>
    Task<ProjectMemberResponse> AcceptExternalInvitationAsync(
        string rawToken, CancellationToken ct = default);
}