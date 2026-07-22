using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

public class TaskAssignment : BaseEntity
{
    public Guid TaskId { get; set; }
    public Guid EmployeeId { get; set; }
    public RoleInTask RoleInTask { get; set; } = RoleInTask.Owner;
    public DateTime AssignedDate { get; set; }

    public TaskItem Task { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
