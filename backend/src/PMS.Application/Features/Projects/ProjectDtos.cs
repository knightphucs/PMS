using PMS.Domain.Enums;

namespace PMS.Application.Features.Projects;

public record CreateProjectRequest(string Name, string Description, DateTime ExpectedCompletionDate);
public record UpdateProjectRequest(string Name, string Description, DateTime ExpectedCompletionDate, byte[] RowVersion);

/// <summary>
/// Phần tử của danh sách project.
///
/// <para>
/// <b>Vì sao có <c>RoleInProject</c> ở đây:</c></b> vai trò không phải thuộc tính của
/// project mà phụ thuộc người đang hỏi — nhưng danh sách này vốn đã lọc theo thành viên
/// nên vai trò đã nằm sẵn trong phép join, chỉ là trước đây không được trả ra. Thiếu nó,
/// UI không có cách nào ẩn/hiện nút Sửa/Xóa đúng theo §10 mà không phải gọi thêm
/// <c>GET /projects/{id}/members</c> cho TỪNG dòng (N+1).
/// </para>
/// </summary>
public record ProjectSummaryResponse(
    Guid Id, string Name, Status Status, DateTime ExpectedCompletionDate,
    RoleInProject RoleInProject);

public record ProjectDetailResponse(
    Guid Id, string Name, string Description, Status Status,
    DateTime ExpectedCompletionDate, IReadOnlyList<ProjectMemberResponse> Members, byte[] RowVersion);

public record ProjectMemberResponse(
    Guid EmployeeId, string EmployeeName, RoleInProject RoleInProject, InvitationStatus InvitationStatus, DateTime? JoinedDate);