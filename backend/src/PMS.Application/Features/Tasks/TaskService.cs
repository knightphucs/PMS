using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Tasks;

public class TaskService : ITaskService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly TaskMapper _mapper;
    private readonly ILogger<TaskService> _logger;

    public TaskService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        IActivityLogger activityLog, TaskMapper mapper, ILogger<TaskService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<TaskSummaryResponse> CreateAsync(
        CreateTaskRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(request.ProjectId, ProjectAction.CreateTask, ct);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            ProjectId = request.ProjectId,
            ReporterId = _currentUser.RequireEmployeeId(),
            DueDate = request.DueDate,
            Priority = request.Priority
        };

        if (request.SprintId is { } sprintId)
            task.SprintId = await RequireSprintOfProjectAsync(sprintId, request.ProjectId, ct);

        if (request.ParentTaskId is { } parentId)
        {
            var parent = await _uow.Tasks.GetWithSubtasksAsync(parentId, ct)
                ?? throw new NotFoundException(nameof(TaskItem), parentId);

            if (parent.ProjectId != request.ProjectId)
                throw new BusinessRuleException(
                    "Task cha thuộc project khác — subtask phải nằm cùng project với task cha.");

            // Domain tự chặn subtask 2 cấp bằng DomainException (409) và tự gán
            // ParentTaskId + ProjectId cho con.
            parent.AddSubtask(task);
        }

        await _uow.Tasks.AddAsync(task, ct);

        _activityLog.Log(nameof(TaskItem), task.Id, ActivityAction.Created,
            task.IsSubtask
                ? $"Tạo subtask '{task.Name}' của task {task.ParentTaskId}"
                : $"Tạo task '{task.Name}' (độ ưu tiên {task.Priority})");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Tạo task {TaskId} trong project {ProjectId} bởi {EmployeeId}",
            task.Id, task.ProjectId, _currentUser.EmployeeId);

        return _mapper.ToSummary(task);
    }

    public async Task<TaskDetailResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var task = await _uow.Tasks.GetWithDetailsAsync(id, ct)
            ?? throw new NotFoundException(nameof(TaskItem), id);

        await _authz.AuthorizeTaskAsync(task, ProjectAction.View, ct);

        return _mapper.ToDetail(task);
    }

    public async Task<PagedResult<TaskSummaryResponse>> GetByProjectAsync(
        Guid projectId, PagedRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);

        var paged = await _uow.Tasks.GetPagedByProjectAsync(projectId, request, ct);

        return paged.Map(_mapper.ToSummary);
    }

    public async Task<TaskDetailResponse> UpdateAsync(
        Guid id, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var task = await _uow.Tasks.GetWithDetailsAsync(id, ct)
            ?? throw new NotFoundException(nameof(TaskItem), id);

        await _authz.AuthorizeTaskAsync(task, ProjectAction.UpdateTask, ct);

        // ADR-016: so với đúng version client đang thấy, không phải version vừa load.
        _uow.SetConcurrencyToken(task, request.RowVersion);

        task.Name = request.Name.Trim();
        task.DueDate = request.DueDate;
        task.Priority = request.Priority;

        _activityLog.Log(nameof(TaskItem), id, ActivityAction.Updated,
            $"Cập nhật task '{task.Name}'");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Cập nhật task {TaskId} bởi {EmployeeId}",
            id, _currentUser.EmployeeId);

        return _mapper.ToDetail(task);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var task = await _uow.Tasks.GetWithSubtasksAsync(id, ct)
            ?? throw new NotFoundException(nameof(TaskItem), id);

        await _authz.AuthorizeTaskAsync(task, ProjectAction.DeleteTask, ct);

        // ADR-018: chặn thay vì cascade. Subtask là công việc con cùng cấp chi tiết với
        // task cha (không phải "hạ tầng đi kèm" như quan hệ Project->Task ở ADR-008),
        // nên rủi ro mất dữ liệu có ý nghĩa nếu cascade là cao hơn.
        var activeSubtasks = task.Subtasks.Count(s => s.Status != Status.Done);
        if (activeSubtasks > 0)
            throw new ConflictException(
                $"Không thể xóa task khi còn {activeSubtasks} subtask chưa hoàn thành. " +
                "Hãy hoàn thành hoặc xóa các subtask đó trước.");

        // Cascade tường minh xuống subtask đã Done: ApplySoftDelete() đổi state
        // Deleted -> Modified TRƯỚC khi SaveChanges chạy, nên cascade tự động của
        // EF Core không kích hoạt (bài học ADR-008).
        foreach (var subtask in task.Subtasks) _uow.Tasks.Remove(subtask);
        _uow.Tasks.Remove(task);

        _activityLog.Log(nameof(TaskItem), id, ActivityAction.Deleted,
            $"Xóa task '{task.Name}' cùng {task.Subtasks.Count} subtask đã hoàn thành");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Xóa mềm task {TaskId} bởi {EmployeeId}: {SubtaskCount} subtask",
            id, _currentUser.EmployeeId, task.Subtasks.Count);
    }

    public async Task<TaskSummaryResponse> MoveToSprintAsync(
        Guid id, MoveTaskToSprintRequest request, CancellationToken ct = default)
    {
        // GetWithSubtasksAsync chứ không phải GetByIdAsync: ToSummary trả SubtaskProgress,
        // mà collection chưa nạp sẽ rỗng nên task cha có subtask bị báo nhầm 0%.
        var task = await _uow.Tasks.GetWithSubtasksAsync(id, ct)
            ?? throw new NotFoundException(nameof(TaskItem), id);

        await _authz.AuthorizeTaskAsync(task, ProjectAction.ManageSprint, ct);

        string destination;
        if (request.SprintId is { } sprintId)
        {
            var sprint = await RequireSprintOfProjectAsync(sprintId, task.ProjectId, ct);
            task.SprintId = sprint;
            destination = $"sprint {sprintId}";
        }
        else
        {
            task.SprintId = null;
            destination = "Backlog";
        }

        _activityLog.Log(nameof(TaskItem), id, ActivityAction.Updated,
            $"Chuyển task '{task.Name}' sang {destination}");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Chuyển task {TaskId} sang {Destination} bởi {EmployeeId}",
            id, destination, _currentUser.EmployeeId);

        return _mapper.ToSummary(task);
    }

    public async Task<IReadOnlyList<TaskSummaryResponse>> GetBacklogAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);

        var backlog = await _uow.Tasks.GetBacklogAsync(projectId, ct);

        return backlog.Select(_mapper.ToSummary).ToList();
    }

    public async Task<BoardResponse> GetBoardAsync(
        Guid projectId, Guid? sprintId, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);

        IReadOnlyList<TaskItem> tasks;
        if (sprintId is { } id)
        {
            await RequireSprintOfProjectAsync(id, projectId, ct);

            // GetBySprintAsync trả cả subtask; board chỉ hiển thị task gốc, subtask nằm
            // trong chi tiết task cha.
            tasks = (await _uow.Tasks.GetBySprintAsync(id, ct))
                    .Where(t => t.ParentTaskId is null)
                    .ToList();
        }
        else
        {
            tasks = await _uow.Tasks.GetRootTasksByProjectAsync(projectId, ct);
        }

        // Duyệt theo Enum.GetValues chứ không theo GroupBy dữ liệu: board phải luôn có đủ
        // 4 cột kể cả cột rỗng, nếu không frontend Kanban lại phải tự dựng cột thiếu.
        var columns = Enum.GetValues<Status>()
            .Select(status => new BoardColumn(
                status,
                tasks.Where(t => t.Status == status).Select(_mapper.ToSummary).ToList()))
            .ToList();

        return new BoardResponse(projectId, sprintId, columns);
    }

    /// <summary>
    /// Sprint phải tồn tại và thuộc đúng project của task — nếu không sẽ tạo ra task nằm
    /// trong sprint của project khác, thứ mà FK không chặn được (Task giữ cả ProjectId
    /// lẫn SprintId một cách độc lập).
    /// </summary>
    private async Task<Guid> RequireSprintOfProjectAsync(
        Guid sprintId, Guid projectId, CancellationToken ct)
    {
        var sprint = await _uow.Sprints.GetByIdAsync(sprintId, ct)
            ?? throw new NotFoundException(nameof(Sprint), sprintId);

        if (sprint.ProjectId != projectId)
            throw new BusinessRuleException("Sprint không thuộc project của task này.");

        return sprint.Id;
    }
}
