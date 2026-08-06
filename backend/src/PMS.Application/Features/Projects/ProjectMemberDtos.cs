using PMS.Domain.Enums;

namespace PMS.Application.Features.Projects;

public record InviteMemberRequest(string Email, RoleInProject Role);

public record ChangeMemberRoleRequest(RoleInProject Role);

public record MyInvitationResponse(
    Guid ProjectId, string ProjectName, RoleInProject Role, DateTime InvitedAt);

/// <summary>Mời một email vào project qua đường link — KHÔNG đòi hỏi email đã có tài khoản (khác <see cref="InviteMemberRequest"/>).</summary>
public record InviteExternalRequest(string Email, RoleInProject Role);

/// <summary>Trả về sau khi tạo lời mời qua email. KHÔNG mang token thô — token chỉ nằm trong nội dung email.</summary>
public record ExternalInvitationResponse(
    Guid Id, Guid ProjectId, string Email, RoleInProject Role, DateTime ExpiresAt);

/// <summary>Xem trước một lời mời từ token thô trong link — dùng cho trang public trước khi người dùng đăng nhập/đăng ký.</summary>
public record InvitationPreviewResponse(
    Guid ProjectId, string ProjectName, string Email, RoleInProject Role, DateTime ExpiresAt);