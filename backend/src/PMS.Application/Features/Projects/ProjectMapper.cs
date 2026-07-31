using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.Projects;

[Mapper]
public partial class ProjectMapper
{
    /// <summary>
    /// Viết TAY chứ không để Mapperly sinh, và đó là chủ ý.
    ///
    /// <para>
    /// <c>RoleInProject</c> phụ thuộc người đang hỏi nên không suy ra được từ
    /// <see cref="Project"/>. Nếu để Mapperly sinh bản một tham số, call site nào quên
    /// truyền vai trò sẽ nhận giá trị mặc định của enum — mà giá trị đó là
    /// <c>ProjectManager</c> (ordinal 0). Hỏng theo chiều nguy hiểm nhất: UI hiện nút
    /// Sửa/Xóa cho cả <c>Viewer</c>. Bắt buộc hai tham số thì trình biên dịch chặn ngay.
    /// </para>
    /// </summary>
    public ProjectSummaryResponse ToSummary(Project project, RoleInProject roleInProject)
        => new(project.Id, project.Name, project.Status, project.ExpectedCompletionDate, roleInProject);

#pragma warning disable RMG020 // Source member is not mapped to any target member
    public partial ProjectDetailResponse ToDetail(Project project);

    [MapProperty(nameof(ProjectMember.Employee.Name), nameof(ProjectMemberResponse.EmployeeName))]
    public partial ProjectMemberResponse ToMemberResponse(ProjectMember member);

    [MapProperty("Project.Name", nameof(MyInvitationResponse.ProjectName))]
    [MapProperty(nameof(ProjectMember.CreatedAt), nameof(MyInvitationResponse.InvitedAt))]
    [MapProperty(nameof(ProjectMember.RoleInProject), nameof(MyInvitationResponse.Role))]
    public partial MyInvitationResponse ToMyInvitation(ProjectMember member);
#pragma warning restore RMG020 // Source member is not mapped to any target member
}