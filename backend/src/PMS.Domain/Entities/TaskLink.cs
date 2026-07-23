using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

public class TaskLink : BaseEntity
{
    public Guid SourceTaskId { get; set; }
    public Guid TargetTaskId { get; set; }
    public LinkType LinkType { get; set; }

    public TaskItem SourceTask { get; set; } = null!;
    public TaskItem TargetTask { get; set; } = null!;

    public bool IsBlocking() => LinkType == LinkType.Blocks;
}
