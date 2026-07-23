using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

public class TaskItem : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public Status Status { get; private set; } = Status.ToDo;
    public Priority Priority { get; set; } = Priority.Medium;

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

    public void AddAssignee(Employee employee, RoleInTask role)
    {
        if (Assignments.Any(a => a.EmployeeId == employee.Id)) return;
        Assignments.Add(new TaskAssignment
        {
            TaskId = Id, EmployeeId = employee.Id,
            RoleInTask = role, AssignedDate = DateTime.UtcNow
        });
    }

    public void LinkTo(TaskItem target, LinkType linkType)
        => OutgoingLinks.Add(new TaskLink
        {
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
            throw new InvalidOperationException(
                $"Không thể chuyển trạng thái từ {Status} sang {target}.");
        Status = target;
    }

    public bool IsOverdue()
        => DueDate.HasValue
           && DueDate.Value.Date < DateTime.UtcNow.Date
           && Status != Status.Done;

    public decimal SubtaskProgress()
    {
        if (Subtasks.Count == 0) return 0m;
        var done = Subtasks.Count(s => s.Status == Status.Done);
        return Math.Round((decimal)done / Subtasks.Count * 100, 2);
    }

    public void AddSubtask(TaskItem child)
    {
        if (IsSubtask)
            throw new InvalidOperationException(
                "Subtask không được có subtask con (chỉ 1 cấp cha–con).");
        child.ParentTaskId = Id;
        child.ProjectId = ProjectId;
        Subtasks.Add(child);
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}