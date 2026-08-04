using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Application.Features.Tasks;
using PMS.Domain.Entities;

namespace PMS.Application.Features.ActivityLogs;

public class ActivityLogService : IActivityLogService
{
    /// <summary>
    /// 🔴 Danh sách CỐ ĐỊNH ở server các loại entity mà nhật ký cấp hệ thống được đọc
    /// (ADR-042). Không có đường nào cho client mở rộng nó.
    /// <list type="bullet">
    /// <item><c>Employee</c> — khóa/mở tài khoản, đổi SystemRole.</item>
    /// <item><c>Label</c> — sửa/xóa nhãn toàn cục, thao tác chỉ SystemAdmin làm được (ADR-037).</item>
    /// <item><c>RolePermission</c> — đổi tập quyền của một vai trò (ADR-045). Đây là thao tác
    /// nhạy cảm nhất hệ thống; để nó vô hình ở đây thì mâu thuẫn với chính lý do
    /// <c>AdminAuditController</c> tồn tại.</item>
    /// </list>
    /// Thêm <c>Project</c> hay <c>TaskItem</c> vào đây là mở lại đúng "god mode" mà ADR-042
    /// vừa đóng.
    /// </summary>
    private static readonly string[] SystemScopedEntityTypes =
        [nameof(Employee), nameof(Label), nameof(RolePermission)];

    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ActivityLogMapper _mapper;

    public ActivityLogService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ActivityLogMapper mapper)
    {
        _uow = uow;
        _authz = authz;
        _mapper = mapper;
    }

    public async Task<PagedResult<ActivityLogResponse>> GetTaskActivityAsync(
        Guid taskId, PagedRequest request, CancellationToken ct = default)
    {
        var task = await _uow.Tasks.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        // View: đọc lịch sử là quyền của MỌI thành viên kể cả Viewer, đúng khuôn ADR-026
        // đã dùng cho đọc comment.
        await _authz.AuthorizeTaskAsync(task, ProjectAction.View, ct);

        var paged = await _uow.ActivityLogs.GetPagedByEntityAsync(
            nameof(TaskItem), taskId, request, ct);

        return paged.Map(_mapper.ToResponse);
    }

    public async Task<PagedResult<ActivityLogResponse>> GetProjectActivityAsync(
        Guid projectId, PagedRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);

        var paged = await _uow.ActivityLogs.GetPagedByEntityAsync(
            nameof(Project), projectId, request, ct);

        return paged.Map(_mapper.ToResponse);
    }

    public async Task<PagedResult<SystemAuditLogResponse>> GetSystemAuditAsync(
        PagedRequest request, CancellationToken ct = default)
    {
        // Không gọi IProjectAuthorizationService: đây là dữ liệu cấp hệ thống, không thuộc
        // project nào — cùng loại ngoại lệ hợp lệ mà ADR-023 đã dành cho Notification.
        // Chốt chặn là policy `audit:read` ở controller (ADR-045 — trước 2026-08-04 là
        // `require-system-admin`) CỘNG danh sách entity type cố định phía trên.
        var paged = await _uow.ActivityLogs.GetPagedBySystemScopeAsync(
            SystemScopedEntityTypes, request, ct);

        return paged.Map(_mapper.ToAuditResponse);
    }
}
