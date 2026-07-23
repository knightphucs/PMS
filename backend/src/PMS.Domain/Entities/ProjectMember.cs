using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

public class ProjectMember : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid EmployeeId { get; set; }
    public RoleInProject RoleInProject { get; set; }
    public DateTime JoinedDate { get; set; }
    public InvitationStatus InvitationStatus { get; set; } = InvitationStatus.Pending;

    public Project Project { get; set; } = null!;
    public Employee Employee { get; set; } = null!;

    public void Accept()  => InvitationStatus = InvitationStatus.Accepted;
    public void Decline() => InvitationStatus = InvitationStatus.Declined;
    public bool IsActive() => InvitationStatus == InvitationStatus.Accepted;
}
