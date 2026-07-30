using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Tasks;

public class TaskStatusTransitionService : ITaskStatusTransitionService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly INotificationService _notifications;
    private readonly TaskMapper _mapper;
    private readonly ILogger<TaskStatusTransitionService> _logger;

    public TaskStatusTransitionService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        IActivityLogger activityLog, INotificationService notifications,
        TaskMapper mapper, ILogger<TaskStatusTransitionService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _notifications = notifications;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<TaskSummaryResponse> ChangeStatusAsync(
        Guid taskId, ChangeTaskStatusRequest request, CancellationToken ct = default)
    {
        var actorId = _currentUser.RequireEmployeeId();

        var task = await _uow.Tasks.GetForStatusChangeAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        // View là ngưỡng thấp nhất: chỉ để loại người ngoài project (404) và lấy được
        // RoleInProject. Luật thật nằm ở EnsureCanChangeStatus bên dưới.
        var role = await _authz.AuthorizeTaskAsync(task, ProjectAction.View, ct);

        EnsureCanChangeStatus(task, role, actorId);

        if (request.Target == Status.InProgress)
            await EnsureNotBlockedAsync(taskId, ct);

        var previous = task.Status;

        // Nhảy bước -> DomainException = 409. Đứng yên cũng không hợp lệ, nhờ đó hai
        // người cùng chuyển tới một đích thì người sau bị chặn — đó là lý do đổi trạng
        // thái không cần round-trip RowVersion (ADR-021).
        task.ChangeStatus(request.Target);

        _activityLog.Log(nameof(TaskItem), taskId, ActivityAction.StatusChanged,
            $"Đổi trạng thái task '{task.Name}': {previous} -> {request.Target}");

        _notifications.NotifyMany(task.InterestedEmployeeIds(), NotificationType.StatusChanged,
            $"Task '{task.Name}' đã chuyển từ {previous} sang {request.Target}", taskId);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Đổi trạng thái task {TaskId}: {Previous} -> {Target} bởi {ActorId} (role {Role})",
            taskId, previous, request.Target, actorId, role);

        return _mapper.ToSummary(task);
    }

    /// <summary>
    /// ADR-017: Assignee của chính task đó HOẶC ProjectManager của project chứa task.
    /// Member không phải assignee và Viewer đều bị từ chối.
    /// </summary>
    private static void EnsureCanChangeStatus(TaskItem task, RoleInProject role, Guid actorId)
    {
        if (role == RoleInProject.ProjectManager) return;
        if (task.Assignments.Any(a => a.EmployeeId == actorId)) return;

        throw new ForbiddenException(
            "Chỉ người được gán task hoặc ProjectManager của project mới được đổi trạng thái task này.");
    }

    /// <summary>
    /// Task bị chặn khi có TaskLink Blocks/IsBlockedBy trỏ tới một task chưa Done (§5).
    /// Chỉ kiểm khi chuyển sang InProgress — lùi về ToDo hay đóng task thì không cần.
    /// </summary>
    private async Task EnsureNotBlockedAsync(Guid taskId, CancellationToken ct)
    {
        var blockers = await _uow.Tasks.GetUnfinishedBlockersAsync(taskId, ct);
        if (blockers.Count == 0) return;

        var names = string.Join(", ", blockers.Select(b => $"'{b.Name}'"));
        throw new ConflictException(
            $"Không thể bắt đầu task khi còn bị chặn bởi {blockers.Count} task chưa hoàn thành: {names}.");
    }
}
