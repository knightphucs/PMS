using PMS.Application.Common.Models;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Common.Interfaces;

/// <summary>Một project kèm vai trò của người đang truy vấn — xem <see cref="IProjectRepository.GetPagedForEmployeeAsync"/>.</summary>
public record ProjectWithRole(Project Project, RoleInProject RoleInProject);

public interface IProjectRepository : IRepository<Project>
{
    /// <summary>Nạp kèm Members + Employee — cần cho việc kiểm tra quyền (GetRoleOf).</summary>
    Task<Project?> GetWithMembersAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Danh sách project mà employee là thành viên đã Accepted, có phân trang, <b>kèm vai
    /// trò của chính employee đó</b> trong từng project.
    /// </summary>
    /// <remarks>
    /// Trả kèm vai trò thay vì để caller query lại: phép lọc thành viên đã chạm đúng hàng
    /// <c>ProjectMember</c> cần thiết rồi, nên lấy thêm một cột không tốn round-trip nào.
    /// Nếu tách ra thì mỗi dòng danh sách là một query nữa (N+1).
    /// </remarks>
    Task<PagedResult<ProjectWithRole>> GetPagedForEmployeeAsync(
        Guid employeeId, PagedRequest request, CancellationToken ct = default);

    /// <summary>Vai trò của employee trong project (null nếu không phải thành viên Accepted).</summary>
    Task<RoleInProject?> GetRoleInProjectAsync(Guid projectId, Guid employeeId, CancellationToken ct = default);

    /// <summary>Nạp kèm Tasks + Sprints — cần cho việc kiểm tra và cascade khi xóa project.</summary>
    Task<Project?> GetForDeletionAsync(Guid id, CancellationToken ct = default);
}
