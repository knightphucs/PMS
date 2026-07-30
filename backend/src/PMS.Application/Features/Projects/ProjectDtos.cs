using PMS.Domain.Enums;

namespace PMS.Application.Features.Projects;

public record CreateProjectRequest(string Name, string Description, DateTime ExpectedCompletionDate);
public record UpdateProjectRequest(string Name, string Description, DateTime ExpectedCompletionDate, byte[] RowVersion);

public record ProjectSummaryResponse(
    Guid Id, string Name, Status Status, DateTime ExpectedCompletionDate);

public record ProjectDetailResponse(
    Guid Id, string Name, string Description, Status Status,
    DateTime ExpectedCompletionDate, IReadOnlyList<ProjectMemberResponse> Members, byte[] RowVersion);

public record ProjectMemberResponse(
    Guid EmployeeId, string EmployeeName, RoleInProject RoleInProject, InvitationStatus InvitationStatus, DateTime? JoinedDate);