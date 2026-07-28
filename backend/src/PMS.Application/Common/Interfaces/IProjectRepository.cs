using PMS.Application.Common.Models;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Common.Interfaces;

public interface IProjectRepository : IRepository<Project>
{
    /// <summary>Nạp kèm Members + Employee — cần cho việc kiểm tra quyền (GetRoleOf).</summary>
    Task<Project?> GetWithMembersAsync(Guid id, CancellationToken ct = default);

    /// <summary>Danh sách project mà employee là thành viên đã Accepted, có phân trang.</summary>
    Task<PagedResult<Project>> GetPagedForEmployeeAsync(
        Guid employeeId, PagedRequest request, CancellationToken ct = default);

    /// <summary>Vai trò của employee trong project (null nếu không phải thành viên Accepted).</summary>
    Task<RoleInProject?> GetRoleInProjectAsync(Guid projectId, Guid employeeId, CancellationToken ct = default);

    /// <summary>Nạp kèm Tasks + Sprints — cần cho việc kiểm tra và cascade khi xóa project.</summary>
    Task<Project?> GetForDeletionAsync(Guid id, CancellationToken ct = default);
}
