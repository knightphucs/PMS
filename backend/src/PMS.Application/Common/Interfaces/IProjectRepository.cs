using PMS.Application.Common.Models;
using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IProjectRepository : IRepository<Project>
{
    /// <summary>Nạp kèm Members + Employee — cần cho việc kiểm tra quyền (GetRoleOf).</summary>
    Task<Project?> GetWithMembersAsync(Guid id, CancellationToken ct = default);

    /// <summary>Danh sách project mà employee là thành viên đã Accepted, có phân trang.</summary>
    Task<PagedResult<Project>> GetPagedForEmployeeAsync(
        Guid employeeId, PagedRequest request, CancellationToken ct = default);

    /// <summary>Kiểm tra membership nhanh, không nạp cả object (dùng cho authorization).</summary>
    Task<bool> IsActiveMemberAsync(Guid projectId, Guid employeeId, CancellationToken ct = default);
}
