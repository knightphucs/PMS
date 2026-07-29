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
    public byte[] RowVersion { get; set; } = null!;

    public ICollection<TaskItem> Tasks { get; set; } = [];
    public ICollection<Sprint> Sprints { get; set; } = [];

    public bool IsCompleted() => Status == Status.Done;

    public void Complete() => Status = Status.Done;

    public ICollection<ProjectMember> Members { get; set; } = [];

    public ProjectMember Invite(Employee employee, RoleInProject role)
    {
        var existing = Members.FirstOrDefault(m => m.EmployeeId == employee.Id);

        if (existing is not null)
        {
            if (existing.InvitationStatus != InvitationStatus.Declined)
                throw new DomainException(
                    "Người này đã là thành viên hoặc đang có lời mời chờ phản hồi.");

            existing.Reinvite(role);
            return existing;
        }

        var member = ProjectMember.Invite(Id, employee.Id, role);
        Members.Add(member);
        return member;
    }

    public void ChangeMemberRole(Guid employeeId, RoleInProject newRole)
    {
        var member = RequireMember(employeeId);

        if (member.RoleInProject == newRole) return;   // idempotent, không phải lỗi

        // Kiểm TRƯỚC khi đổi: nếu ném exception sau khi đã mutate, entity vẫn nằm trong
        // ChangeTracker ở trạng thái bẩn — một SaveChanges sau đó trên cùng DbContext sẽ
        // ghi xuống DB đúng cái state vừa bị từ chối.
        if (member.RoleInProject == RoleInProject.ProjectManager && member.IsActive())
            EnsureAnotherManagerExists(employeeId);

        member.ChangeRole(newRole);
    }

    public void RemoveMember(Guid employeeId)
    {
        var member = RequireMember(employeeId);

        if (member.RoleInProject == RoleInProject.ProjectManager && member.IsActive())
            EnsureAnotherManagerExists(employeeId);

        Members.Remove(member);
    }

    public RoleInProject? GetRoleOf(Guid employeeId)
        => Members.FirstOrDefault(m => m.EmployeeId == employeeId && m.IsActive())?.RoleInProject;

    private ProjectMember RequireMember(Guid employeeId)
        => Members.FirstOrDefault(m => m.EmployeeId == employeeId)
        ?? throw new DomainException("Người này không có trong danh sách thành viên của project.");

    private void EnsureAnotherManagerExists(Guid excludingEmployeeId)
    {
        var hasOther = Members.Any(m =>
            m.EmployeeId != excludingEmployeeId
            && m.RoleInProject == RoleInProject.ProjectManager
            && m.IsActive());

        if (!hasOther)
            throw new DomainException(
                "Project phải luôn còn ít nhất một Project Manager đang hoạt động.");
    }

    public static Project Create(string name, string description, DateTime expectedCompletionDate, Guid creatorId)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description.Trim(),
            ExpectedCompletionDate = expectedCompletionDate
        };

        project.Members.Add(ProjectMember.CreateOwner(project.Id, creatorId));
        return project;
    }
}
