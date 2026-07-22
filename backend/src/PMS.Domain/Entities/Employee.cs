using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

public class Employee : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public SystemRole SystemRole { get; set; } = SystemRole.User;

    public ICollection<TaskItem> ReportedTasks { get; set; } = [];
    public ICollection<ProjectMember> ProjectMemberships { get; set; } = [];
    public ICollection<TaskAssignment> TaskAssignments { get; set; } = [];
    public ICollection<Watcher> Watching { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<ActivityLog> ActivityLogs { get; set; } = [];
    
    public bool IsSystemAdmin => SystemRole == SystemRole.SystemAdmin;
}