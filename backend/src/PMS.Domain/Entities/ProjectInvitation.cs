using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// Lời mời tham gia project gửi qua email — khác <see cref="ProjectMember"/> ở chỗ người
/// nhận CHƯA CHẮC đã có tài khoản trong hệ thống (không có <c>EmployeeId</c> để gắn vào).
/// <para>
/// Soi gương <see cref="PasswordResetToken"/>: chỉ lưu <b>hash SHA-256</b> của token, không
/// lưu giá trị thô — kẻ đọc được database vẫn không tự nhận lời mời được.
/// </para>
/// </summary>
public class ProjectInvitation : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Email { get; set; } = string.Empty;
    public RoleInProject Role { get; set; }
    public Guid InvitedByEmployeeId { get; set; }

    /// <summary>SHA-256 dạng hex của token thô. 64 ký tự.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? CreatedByIp { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsUsed => UsedAt is not null;

    /// <summary>Còn dùng được không. Ba lý do hỏng gộp làm một — service trả cùng một lỗi cho cả ba (mirror ADR-041).</summary>
    public bool IsUsable => !IsExpired && !IsUsed;

    public bool IsForEmail(string email)
        => string.Equals(Email, email, StringComparison.OrdinalIgnoreCase);

    public void MarkUsed() => UsedAt = DateTime.UtcNow;

    /// <summary>Vô hiệu hóa mà không tiêu dùng — dùng khi mời lại (resend) một email đang có lời mời chờ.</summary>
    public void Invalidate() => UsedAt ??= DateTime.UtcNow;

    public static ProjectInvitation Create(
        Guid projectId, string email, RoleInProject role, Guid invitedByEmployeeId,
        string tokenHash, DateTime expiresAt, string? createdByIp)
        => new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Email = email,
            Role = role,
            InvitedByEmployeeId = invitedByEmployeeId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedByIp = createdByIp,
        };
}
