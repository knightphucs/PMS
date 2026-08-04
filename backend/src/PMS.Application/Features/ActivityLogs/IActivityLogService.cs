using PMS.Application.Common.Models;

namespace PMS.Application.Features.ActivityLogs;

public interface IActivityLogService
{
    Task<PagedResult<ActivityLogResponse>> GetTaskActivityAsync(
        Guid taskId, PagedRequest request, CancellationToken ct = default);

    Task<PagedResult<ActivityLogResponse>> GetProjectActivityAsync(
        Guid projectId, PagedRequest request, CancellationToken ct = default);

    /// <summary>
    /// Nhật ký cấp hệ thống — gác bằng policy <c>audit:read</c> ở controller (ADR-045).
    /// KHÔNG nhận tham số lọc loại entity từ bên ngoài (ADR-042).
    /// </summary>
    Task<PagedResult<SystemAuditLogResponse>> GetSystemAuditAsync(
        PagedRequest request, CancellationToken ct = default);
}
