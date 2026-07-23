namespace PMS.Domain.Entities;

public class Watcher
{
    public Guid TaskId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTime CreatedAt { get; set; }

    public TaskItem Task { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
