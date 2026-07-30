using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

public class TaskItem : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public Status Status { get; private set; } = Status.ToDo;
    public Priority Priority { get; set; } = Priority.Medium;

    public byte[] RowVersion { get; set; } = null!;

    public Guid ProjectId { get; set; }
    public Guid? SprintId { get; set; }
    public Guid? ParentTaskId { get; set; }
    public Guid ReporterId { get; set; }

    public Project Project { get; set; } = null!;
    public Sprint? Sprint { get; set; }
    public TaskItem? ParentTask { get; set; }
    public Employee Reporter { get; set; } = null!;
    public ICollection<TaskItem> Subtasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();
    public ICollection<Watcher> Watchers { get; set; } = new List<Watcher>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Label> Labels { get; set; } = new List<Label>();
    public ICollection<TaskLink> OutgoingLinks { get; set; } = new List<TaskLink>();
    public ICollection<TaskLink> IncomingLinks { get; set; } = new List<TaskLink>();

    // Id phải sinh phía application: PmsDbContext.ApplyIdNeverGenerated() đặt
    // ValueGeneratedNever() cho mọi BaseEntity.Id, nên để mặc định Guid.Empty thì
    // bản ghi thứ hai sẽ vi phạm khóa chính. Nhất quán với ProjectMember.Invite().
    public void AddAssignee(Employee employee, RoleInTask role)
    {
        if (Assignments.Any(a => a.EmployeeId == employee.Id)) return;
        Assignments.Add(new TaskAssignment
        {
            Id = Guid.NewGuid(),
            TaskId = Id, EmployeeId = employee.Id,
            RoleInTask = role, AssignedDate = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Gỡ 1 người khỏi task. Trả về false nếu người đó vốn không được gán —
    /// để Service phân biệt "không có gì để làm" với thao tác thật (chỉ ghi
    /// ActivityLog/Notification khi thật sự có thay đổi).
    /// </summary>
    public bool RemoveAssignee(Guid employeeId)
    {
        var assignment = Assignments.FirstOrDefault(a => a.EmployeeId == employeeId);
        if (assignment is null) return false;

        Assignments.Remove(assignment);
        return true;
    }

    public void LinkTo(TaskItem target, LinkType linkType)
        => OutgoingLinks.Add(new TaskLink
        {
            Id = Guid.NewGuid(),
            SourceTaskId = Id, TargetTaskId = target.Id, LinkType = linkType
        });

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public bool IsSubtask => ParentTaskId is not null;

    public bool CanTransitionTo(Status target) => (Status, target) switch
    {
        (Status.ToDo, Status.InProgress)   => true,
        (Status.InProgress, Status.Review) => true,
        (Status.InProgress, Status.ToDo)   => true,
        (Status.Review, Status.Done)       => true,
        (Status.Review, Status.InProgress) => true, 
        (Status.Done, Status.Review)       => true,
        _ => false
    };

    public void ChangeStatus(Status target)
    {
        if (!CanTransitionTo(target))
            throw new DomainException(
                $"Không thể chuyển trạng thái từ {Status} sang {target}.");
        Status = target;
    }

    // IsOverdue/SubtaskProgress là property computed (không lưu cứng - xem §5 ARCHITECTURE).
    // Để là property thay vì method vì Mapperly chỉ map được property; nhờ đó TaskMapper
    // không cần map thủ công. EF Core bỏ qua property get-only không có backing field —
    // IsSubtask ở dưới đã chứng minh điều đó chạy được với cấu hình hiện tại.
    public bool IsOverdue
        => DueDate.HasValue
           && DueDate.Value.Date < DateTime.UtcNow.Date
           && Status != Status.Done;

    public decimal SubtaskProgress
    {
        get
        {
            if (Subtasks.Count == 0) return 0m;
            var done = Subtasks.Count(s => s.Status == Status.Done);
            return Math.Round((decimal)done / Subtasks.Count * 100, 2);
        }
    }

    public void AddSubtask(TaskItem child)
    {
        // DomainException chứ không phải InvalidOperationException: middleware chỉ map
        // DomainException thành 409, ngoại lệ khác rơi vào catch-all và trả 500 (ADR-011).
        if (IsSubtask)
            throw new DomainException(
                "Subtask không được có subtask con (chỉ 1 cấp cha–con).");
        child.ParentTaskId = Id;
        child.ProjectId = ProjectId;
        Subtasks.Add(child);
    }
}