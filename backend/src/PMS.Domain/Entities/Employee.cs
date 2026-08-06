using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

public class Employee : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public SystemRole SystemRole { get; private set; } = SystemRole.User;
    public bool IsLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    public string? LockReason { get; set; }

    public ICollection<TaskItem> ReportedTasks { get; set; } = [];
    public ICollection<ProjectMember> ProjectMemberships { get; set; } = [];
    public ICollection<TaskAssignment> TaskAssignments { get; set; } = [];
    public ICollection<Watcher> Watching { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<ActivityLog> ActivityLogs { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    
    public bool IsSystemAdmin => SystemRole == SystemRole.SystemAdmin;

    public void Lock(string reason)
    {
        if (IsLocked)
            throw new DomainException("Tài khoản đã bị khoá từ trước.");
        
        IsLocked = true;
        LockedAt = DateTime.UtcNow;
        LockReason = reason.Trim();
    }

    public void Unlock()
    {
        if (!IsLocked)
            throw new DomainException("Tài khoản đang không bị khóa.");

        IsLocked = false;
        LockedAt = null;
        LockReason = null;
    }

    public void ChangeSystemRole(SystemRole role)
    {
        if (SystemRole == role) return;   // idempotent
        SystemRole = role;
    }

    /// <summary>
    /// Tự đổi tên hiển thị (ADR-049, đường ghi hồ sơ cá nhân). Cùng khuôn với
    /// <see cref="Lock"/>/<see cref="ChangeSystemRole"/>: validate + gán, không có gì khác.
    /// </summary>
    public void Rename(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            throw new DomainException("Tên không được để trống.");

        Name = trimmed;
    }

    public static Employee Register(string name, string email, string passwordHash) => new()
    {
        Id = Guid.NewGuid(),
        Name = name.Trim(),
        Email = email.Trim(),
        PasswordHash = passwordHash,
        SystemRole = SystemRole.User
    };
}