using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Tasks;
using PMS.Domain.Entities;

namespace PMS.Application.Features.Watchers;

/// <summary>
/// Đăng ký theo dõi task để nhận thông báo dù không được gán (ADR-036).
/// <para>
/// Cả ba vai trò đều theo dõi được, kể cả <c>Viewer</c> — đây là thao tác ghi duy nhất
/// Viewer làm được, và hợp lý vì nó chỉ ảnh hưởng hộp thông báo của chính người đó.
/// Vẫn dùng <c>ProjectAction.Watch</c> riêng chứ không mượn <c>View</c>: <c>View</c>
/// không bao giờ được phép cho qua một mutation, kể cả mutation vô hại.
/// </para>
/// <para>
/// KHÔNG ghi <c>ActivityLog</c> — cùng lý do ADR-023 không ghi log khi đọc thông báo:
/// "tôi bấm theo dõi" không phải thay đổi nghiệp vụ trên task, và ghi lại mỗi lần bấm
/// sẽ làm loãng chính cái audit trail đó.
/// </para>
/// </summary>
public class WatcherService : IWatcherService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ICurrentUserService _currentUser;
    private readonly WatcherMapper _mapper;
    private readonly ILogger<WatcherService> _logger;

    public WatcherService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        WatcherMapper mapper, ILogger<WatcherService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WatcherResponse>> GetByTaskAsync(
        Guid taskId, CancellationToken ct = default)
    {
        await LoadAndAuthorizeAsync(taskId, ProjectAction.View, ct);

        return (await _uow.Watchers.ListByTaskAsync(taskId, ct))
            .Select(_mapper.ToResponse).ToList();
    }

    public async Task<WatchStateResponse> WatchAsync(Guid taskId, CancellationToken ct = default)
    {
        await LoadAndAuthorizeAsync(taskId, ProjectAction.Watch, ct);
        var employeeId = _currentUser.RequireEmployeeId();

        // Idempotent: bấm "Theo dõi" hai lần không phải vi phạm nghiệp vụ, nên trả trạng
        // thái hiện tại thay vì 409 (tiền lệ Notification.MarkAsRead, ADR-023).
        if (!await _uow.Watchers.ExistsAsync(taskId, employeeId, ct))
        {
            // 🔴 CreatedAt phải set TAY: Watcher không phải BaseEntity nên
            // ApplyAuditFields() bỏ qua nó, và WatcherConfiguration không có default value.
            // Thiếu dòng này thì mọi watcher mang mốc 0001-01-01 và OrderBy(CreatedAt)
            // trong ListByTaskAsync trở nên vô nghĩa.
            _uow.Watchers.Add(new Watcher
            {
                TaskId = taskId,
                EmployeeId = employeeId,
                CreatedAt = DateTime.UtcNow
            });

            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("{EmployeeId} bắt đầu theo dõi task {TaskId}", employeeId, taskId);
        }

        return await BuildStateAsync(taskId, employeeId, ct);
    }

    public async Task<WatchStateResponse> UnwatchAsync(Guid taskId, CancellationToken ct = default)
    {
        await LoadAndAuthorizeAsync(taskId, ProjectAction.Watch, ct);
        var employeeId = _currentUser.RequireEmployeeId();

        var watcher = await _uow.Watchers.GetAsync(taskId, employeeId, ct);
        if (watcher is not null)
        {
            _uow.Watchers.Remove(watcher);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("{EmployeeId} bỏ theo dõi task {TaskId}", employeeId, taskId);
        }

        return await BuildStateAsync(taskId, employeeId, ct);
    }

    private async Task<WatchStateResponse> BuildStateAsync(
        Guid taskId, Guid employeeId, CancellationToken ct)
    {
        var watchers = await _uow.Watchers.ListByTaskAsync(taskId, ct);
        return new WatchStateResponse(
            watchers.Any(w => w.EmployeeId == employeeId),
            watchers.Count);
    }

    /// <summary>
    /// Nạp task rồi kiểm quyền project-scoped. <c>AuthorizeTaskAsync</c> chuẩn hóa 404 về
    /// task (ADR-019) nên người ngoài project không dò được taskId có tồn tại hay không.
    /// </summary>
    private async Task LoadAndAuthorizeAsync(Guid taskId, ProjectAction action, CancellationToken ct)
    {
        var task = await _uow.Tasks.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        await _authz.AuthorizeTaskAsync(task, action, ct);
    }
}
