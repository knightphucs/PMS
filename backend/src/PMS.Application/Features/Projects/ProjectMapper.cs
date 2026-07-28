using PMS.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.Projects;

[Mapper]
public partial class ProjectMapper
{
#pragma warning disable RMG020 // Source member is not mapped to any target member
    public partial ProjectSummaryResponse ToSummary(Project project);
    public partial ProjectDetailResponse ToDetail(Project project);

    [MapProperty(nameof(ProjectMember.Employee.Name), nameof(ProjectMemberResponse.EmployeeName))]
    public partial ProjectMemberResponse ToMemberResponse(ProjectMember member);

    [MapProperty("Project.Name", nameof(MyInvitationResponse.ProjectName))]
    [MapProperty(nameof(ProjectMember.CreatedAt), nameof(MyInvitationResponse.InvitedAt))]
    [MapProperty(nameof(ProjectMember.RoleInProject), nameof(MyInvitationResponse.Role))]
    public partial MyInvitationResponse ToMyInvitation(ProjectMember member);
#pragma warning restore RMG020 // Source member is not mapped to any target member
}