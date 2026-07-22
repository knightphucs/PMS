using PMS.Domain.Common;

namespace PMS.Domain.Entities;

public class Comment : BaseEntity
{
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Guid TaskId { get; set; }
    public Guid EmployeeId { get; set; }

    public TaskItem Task { get; set; } = null!;
    public Employee Author { get; set; } = null!;
}