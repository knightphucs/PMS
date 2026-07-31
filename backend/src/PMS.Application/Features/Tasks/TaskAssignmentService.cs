using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Tasks;

public class TaskAssignmentService : ITaskAssignmentService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly INotificationService _notifications;
    private readonly TaskMapper _mapper;
    private readonly ILogger<TaskAssignmentService> _logger;

    public TaskAssignmentService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        IActivityLogger activityLog, INotificationService notifications,
        TaskMapper mapper, ILogger<TaskAssignmentService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _notifications = notifications;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TaskAssigneeResponse>> GetAssigneesAsync(
        Guid taskId, CancellationToken ct = default)
    {
        var task = await LoadTaskAsync(taskId, ct);
        await _authz.AuthorizeTaskAsync(task, ProjectAction.View, ct);

        return task.Assignments.Select(_mapper.ToAssigneeResponse).ToList();
    }

    public async Task<TaskAssigneeResponse> AssignAsync(
        Guid taskId, AssignTaskRequest request, CancellationToken ct = default)
    {
        var task = await LoadTaskAsync(taskId, ct);
        await _authz.AuthorizeTaskAsync(task, ProjectAction.ManageAssignees, ct);

        var project = await LoadProjectAsync(task.ProjectId, ct);
        var invitee = RequireActiveMember(project, request.EmployeeId);

        if (task.Assignments.Any(a => a.EmployeeId == request.EmployeeId))
            throw new ConflictException(
                $"{invitee.Name} đã được gán vào task này rồi.");

        task.AddAssignee(invitee, request.Role);

        _activityLog.Log(nameof(TaskItem), taskId, ActivityAction.Assigned,
            $"Gán {invitee.Name} vào task '{task.Name}' với vai trò {request.Role}");

        _notifications.Notify(invitee.Id, NotificationType.TaskAssigned,
            $"Bạn được gán vào task '{task.Name}' với vai trò {request.Role}", taskId);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Gán {EmployeeId} vào task {TaskId} bởi {ActorId}",
            request.EmployeeId, taskId, _currentUser.EmployeeId);

        return _mapper.ToAssigneeResponse(
            task.Assignments.Single(a => a.EmployeeId == request.EmployeeId));
    }

    public async Task<TaskAssigneeResponse> SelfAssignAsync(
        Guid taskId, CancellationToken ct = default)
    {
        var actorId = _currentUser.RequireEmployeeId();

        var task = await LoadTaskAsync(taskId, ct);

        await _authz.AuthorizeTaskAsync(task, ProjectAction.SelfAssign, ct);

        if (task.Status != Status.ToDo)
            throw new ConflictException(
                $"Chỉ tự nhận được task đang ở trạng thái ToDo; task này đang {task.Status}. " +
                "Hãy nhờ ProjectManager gán nếu vẫn cần tham gia.");

        if (task.Assignments.Any(a => a.EmployeeId == actorId))
            throw new ConflictException("Bạn đã nhận task này rồi.");

        var project = await LoadProjectAsync(task.ProjectId, ct);
        var self = RequireActiveMember(project, actorId);

        task.AddAssignee(self, RoleInTask.Owner);

        _activityLog.Log(nameof(TaskItem), taskId, ActivityAction.Assigned,
            $"{self.Name} tự nhận task '{task.Name}'");

        // §5: mọi hành động assign/unassign đều báo cho PM, để PM nắm được ai đang làm gì
        // dù không tự tay gán. NotifyMany tự loại người thực hiện nếu chính họ là PM.
        _notifications.NotifyMany(ActiveManagerIds(project), NotificationType.TaskAssigned,
            $"{self.Name} đã tự nhận task '{task.Name}'", taskId);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("{ActorId} tự nhận task {TaskId}", actorId, taskId);

        return _mapper.ToAssigneeResponse(
            task.Assignments.Single(a => a.EmployeeId == actorId));
    }

    public async Task UnassignAsync(Guid taskId, Guid employeeId, CancellationToken ct = default)
    {
        var actorId = _currentUser.RequireEmployeeId();
        var isSelfUnassign = employeeId == actorId;

        var task = await LoadTaskAsync(taskId, ct);

        // Một endpoint, hai nhánh quyền — cùng khuôn với ProjectMemberService.RemoveMemberAsync.
        await _authz.AuthorizeTaskAsync(
            task, isSelfUnassign ? ProjectAction.SelfAssign : ProjectAction.ManageAssignees, ct);

        var assignment = task.Assignments.FirstOrDefault(a => a.EmployeeId == employeeId)
            ?? throw new NotFoundException("Nhân sự này không được gán vào task.");
        var employeeName = assignment.Employee.Name;

        task.RemoveAssignee(employeeId);

        _uow.TaskAssignments.Remove(assignment);

        _activityLog.Log(nameof(TaskItem), taskId, ActivityAction.Unassigned,
            isSelfUnassign
                ? $"{employeeName} tự rút khỏi task '{task.Name}'"
                : $"Gỡ {employeeName} khỏi task '{task.Name}'");

        if (isSelfUnassign)
        {
            var project = await LoadProjectAsync(task.ProjectId, ct);
            _notifications.NotifyMany(ActiveManagerIds(project), NotificationType.TaskUnassigned,
                $"{employeeName} đã tự rút khỏi task '{task.Name}'", taskId);
        }
        else
        {
            _notifications.Notify(employeeId, NotificationType.TaskUnassigned,
                $"Bạn đã bị gỡ khỏi task '{task.Name}'", taskId);
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Gỡ {EmployeeId} khỏi task {TaskId} bởi {ActorId} (selfUnassign={SelfUnassign})",
            employeeId, taskId, actorId, isSelfUnassign);
    }

    private async Task<TaskItem> LoadTaskAsync(Guid taskId, CancellationToken ct)
        => await _uow.Tasks.GetWithAssignmentsAsync(taskId, ct)
           ?? throw new NotFoundException(nameof(TaskItem), taskId);

    private async Task<Project> LoadProjectAsync(Guid projectId, CancellationToken ct)
        => await _uow.Projects.GetWithMembersAsync(projectId, ct)
           ?? throw new NotFoundException(nameof(Project), projectId);

    /// <summary>
    /// Assignee bắt buộc là ProjectMember đã Accepted của đúng project chứa task (seq-02).
    /// Chặn ở đây chứ không ở domain vì TaskItem không có nav property tới ProjectMember.
    /// </summary>
    private static Employee RequireActiveMember(Project project, Guid employeeId)
    {
        var member = project.Members.FirstOrDefault(m => m.EmployeeId == employeeId && m.IsActive())
            ?? throw new ForbiddenException(
                "Chỉ gán được task cho thành viên đã tham gia project (InvitationStatus = Accepted).");

        return member.Employee;
    }

    private static IEnumerable<Guid> ActiveManagerIds(Project project)
        => project.Members
                  .Where(m => m.RoleInProject == RoleInProject.ProjectManager && m.IsActive())
                  .Select(m => m.EmployeeId);
}
