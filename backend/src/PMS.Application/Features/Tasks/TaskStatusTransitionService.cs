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

        // View là ngưỡng thấp nhất: chỉ để loại người ngoài project (404)
        var role = await _authz.AuthorizeTaskAsync(task, ProjectAction.View, ct);

        EnsureCanChangeStatus(task, role, actorId);

        var target = await _uow.BoardColumns.GetByIdAsync(request.TargetColumnId, ct)
            ?? throw new NotFoundException(nameof(BoardColumn), request.TargetColumnId);

        // Cột của project khác -> DomainException (409) từ chính entity. Kiểm ở domain chứ
        // không ở đây vì đó là bất biến của TaskItem, không phải luật của riêng use case này.
        if (target.ProjectId != task.ProjectId)
            throw new NotFoundException(nameof(BoardColumn), request.TargetColumnId);

        // Kéo về đúng cột đang đứng: không còn là lỗi 409 như thời enum (ADR-052 thay ADR-021).
        // Với cột do người dùng tạo thì "đứng yên" chỉ là một thao tác thừa, không phải một
        // bước đi không hợp lệ — và frontend vốn đã chặn nó bằng cấu trúc.
        if (task.BoardColumnId == target.Id)
            return _mapper.ToSummary(task, await RequireProjectKeyAsync(task.ProjectId, ct));

        // Guard duy nhất còn sót lại của state machine cũ: không bắt đầu một task đang bị chặn.
        // ⚠️ Điều kiện đổi từ "target == InProgress" sang "NHÓM của cột đích là InProgress",
        // nên một cột tự đặt tên "Chờ QA" thuộc nhóm InProgress cũng được bảo vệ. Hệ quả:
        // cột "Đang duyệt" mặc định nay cũng bị kiểm, trước đây thì không — đúng hơn về
        // nghiệp vụ (đẩy task bị chặn đi tiếp vẫn là đẩy task bị chặn).
        if (target.Category == StatusCategory.InProgress)
            await EnsureNotBlockedAsync(taskId, ct);

        var previous = task.BoardColumn.Name;

        task.MoveTo(target);

        _activityLog.Log(nameof(TaskItem), taskId, ActivityAction.StatusChanged,
            $"Đổi trạng thái task '{task.Name}': {previous} -> {target.Name}");

        _notifications.NotifyMany(task.InterestedEmployeeIds(), NotificationType.StatusChanged,
            $"Task '{task.Name}' đã chuyển từ {previous} sang {target.Name}", taskId);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Đổi trạng thái task {TaskId}: {Previous} -> {Target} bởi {ActorId} (role {Role})",
            taskId, previous, target.Name, actorId, role);

        return _mapper.ToSummary(task, await RequireProjectKeyAsync(task.ProjectId, ct));
    }

    private async Task<string> RequireProjectKeyAsync(Guid projectId, CancellationToken ct)
        => await _uow.Projects.GetKeyAsync(projectId, ct)
           ?? throw new NotFoundException(nameof(Project), projectId);

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
