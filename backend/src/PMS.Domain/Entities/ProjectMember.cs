using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

public class ProjectMember : BaseEntity
{
    public ProjectMember() { }
    public Guid ProjectId { get; set; }
    public Guid EmployeeId { get; set; }
    public RoleInProject RoleInProject { get; private set; }
    public DateTime? JoinedDate { get; set; }
    public InvitationStatus InvitationStatus { get; private set; } = InvitationStatus.Pending;

    public Project Project { get; set; } = null!;
    public Employee Employee { get; set; } = null!;

    internal static ProjectMember Invite(Guid projectId, Guid employeeId, RoleInProject role)
        => new()
        {
            Id = Guid.NewGuid(), ProjectId = projectId, EmployeeId = employeeId,
            RoleInProject = role, InvitationStatus = InvitationStatus.Pending
        };

    internal static ProjectMember CreateOwner(Guid projectId, Guid employeeId)
        => new()
        {
            Id = Guid.NewGuid(), ProjectId = projectId, EmployeeId = employeeId,
            RoleInProject = RoleInProject.ProjectManager,
            InvitationStatus = InvitationStatus.Accepted,
            JoinedDate = DateTime.UtcNow
        };

    public void Accept()
    {
        RequirePending();
        InvitationStatus = InvitationStatus.Accepted;
        JoinedDate = DateTime.UtcNow;
    }

    public void Decline()
    {
        RequirePending();
        InvitationStatus = InvitationStatus.Declined;
    }

    internal void Reinvite(RoleInProject role)
    {
        if (InvitationStatus != InvitationStatus.Declined)
            throw new DomainException("Chỉ mời lại được người đã từ chối lời mời trước đó.");

        RoleInProject = role;
        InvitationStatus = InvitationStatus.Pending;
    }

    internal void ChangeRole(RoleInProject role) => RoleInProject = role;

    public bool IsActive() => InvitationStatus == InvitationStatus.Accepted;

    private void RequirePending()
    {
        if (InvitationStatus != InvitationStatus.Pending)
            throw new DomainException(
                $"Lời mời đang ở trạng thái {InvitationStatus}, không thể phản hồi lại.");
    }
}
