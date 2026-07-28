using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

public class Project : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ExpectedCompletionDate { get; set; }
    public Status Status { get; private set; } = Status.ToDo;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<TaskItem> Tasks { get; set; } = [];
    public ICollection<Sprint> Sprints { get; set; } = [];

    public bool IsCompleted() => Status == Status.Done;

    public void Complete() => Status = Status.Done;

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    public ICollection<ProjectMember> Members { get; set; } = [];

    public ProjectMember AddMember(Employee employee, RoleInProject role)
    {
        var member = new ProjectMember
        {
            ProjectId = Id, EmployeeId = employee.Id, RoleInProject = role,
            JoinedDate = DateTime.UtcNow, InvitationStatus = InvitationStatus.Pending
        };
        Members.Add(member);
        return member;
    }

    public RoleInProject? GetRoleOf(Guid employeeId)
        => Members
            .FirstOrDefault(m => m.EmployeeId == employeeId && m.IsActive())
            ?.RoleInProject;

    public static Project Create(string name, string description, DateTime expectedCompletionDate, Guid creatorId)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description.Trim(),
            ExpectedCompletionDate = expectedCompletionDate
        };

        project.Members.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            EmployeeId = creatorId,
            RoleInProject = RoleInProject.ProjectManager,
            InvitationStatus = InvitationStatus.Accepted,
            JoinedDate = DateTime.UtcNow
        });

        return project;
    }
}
