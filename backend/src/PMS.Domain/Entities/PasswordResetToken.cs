using PMS.Domain.Common;

namespace PMS.Domain.Entities;

/// <summary>
/// Token đặt lại mật khẩu — dùng MỘT lần, hạn ngắn (ADR-041).
/// <para>
/// Soi gương <see cref="RefreshToken"/>: chỉ lưu <b>hash SHA-256</b>, không lưu giá trị thô.
/// Kẻ đọc được database vẫn không đổi được mật khẩu của ai — cùng lý do
/// <see cref="Employee.PasswordHash"/> tồn tại.
/// </para>
/// </summary>
public class PasswordResetToken : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>SHA-256 dạng hex của token thô. 64 ký tự.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? CreatedByIp { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsUsed => UsedAt is not null;

    /// <summary>Còn dùng được không. Ba lý do hỏng gộp làm một — service trả cùng một lỗi cho cả ba (ADR-041).</summary>
    public bool IsUsable => !IsExpired && !IsUsed;

    public void MarkUsed() => UsedAt = DateTime.UtcNow;

    /// <summary>Vô hiệu hóa mà không tiêu dùng — dùng khi thu hồi các token còn treo.</summary>
    public void Invalidate() => UsedAt ??= DateTime.UtcNow;
}
