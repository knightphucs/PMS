using PMS.Application.Common.Models;
using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IActivityLogRepository : IRepository<ActivityLog>
{
    /// <summary>
    /// Lịch sử của một entity cụ thể, mới nhất trước, kèm <c>Employee</c> để hiện tên người
    /// thực hiện.
    /// </summary>
    /// <remarks>
    /// ⚠️ <c>SprintService</c> ghi log với <c>EntityType = nameof(Project)</c> và
    /// <c>EntityId = projectId</c>, nên feed của một project bao gồm cả hoạt động sprint.
    /// Đó là kết quả mong muốn, nhưng nghĩa là "project activity" ≠ "những gì làm lên đúng
    /// hàng Project".
    /// </remarks>
    Task<PagedResult<ActivityLog>> GetPagedByEntityAsync(
        string entityType, Guid entityId, PagedRequest request, CancellationToken ct = default);

    /// <summary>
    /// Nhật ký CẤP HỆ THỐNG cho SystemAdmin (ADR-042). Danh sách <paramref name="entityTypes"/>
    /// do <b>server</b> quyết định, tuyệt đối không nhận từ query param — nhận từ client là
    /// biến endpoint này thành kênh đọc vào activity của mọi project và task, tức phủ định
    /// đúng quyết định "SystemAdmin không có đặc quyền nghiệp vụ".
    /// </summary>
    Task<PagedResult<ActivityLog>> GetPagedBySystemScopeAsync(
        IReadOnlyCollection<string> entityTypes, PagedRequest request, CancellationToken ct = default);
}
