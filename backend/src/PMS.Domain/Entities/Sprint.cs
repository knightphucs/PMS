using PMS.Domain.Common;

namespace PMS.Domain.Entities;

public class Sprint : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

    public bool IsActive()
    {
        var today = DateTime.UtcNow.Date;
        return today >= StartDate.Date && today <= EndDate.Date;
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
