using PMS.Domain.Common;

namespace PMS.Domain.Entities;

public class Label : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<TaskItem> Tasks { get; set; } = [];
}