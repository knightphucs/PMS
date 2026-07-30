using PMS.Domain.Common;

namespace PMS.Domain.Entities;

public class Sprint : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

    // Property computed (không lưu cứng), cùng lý do với TaskItem.IsOverdue:
    // Mapperly chỉ map được property, không map method.
    public bool IsActive
    {
        get
        {
            var today = DateTime.UtcNow.Date;
            return today >= StartDate.Date && today <= EndDate.Date;
        }
    }

    public void AddTask(TaskItem task)
    {
        task.SprintId = Id;
        Tasks.Add(task);
    }

    public void RemoveTask(TaskItem task)
    {
        task.SprintId = null;
        Tasks.Remove(task);
    }
}
