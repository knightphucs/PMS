using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Application.Features.BoardColumns;
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

        // Cột trái nhất của project (ADR-052). Project luôn có ít nhất một cột — bốn cột mặc
        // định được cấp lúc tạo project và không xóa được cột cuối cùng — nên `null` ở đây
        // nghĩa là dữ liệu đã hỏng chứ không phải một trạng thái hợp lệ cần xử lý mềm.
        var defaultColumn = await _uow.BoardColumns.GetDefaultForProjectAsync(request.ProjectId, ct)
            ?? throw new NotFoundException(nameof(BoardColumn), request.ProjectId);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = Normalize(request.Description),
            ProjectId = request.ProjectId,
            ReporterId = _currentUser.RequireEmployeeId(),
            DueDate = request.DueDate,
            Priority = request.Priority
        };

        task.MoveTo(defaultColumn);

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

        // 🔴 Caller ĐẦU TIÊN của ExecuteInTransactionAsync kể từ ADR-007 — và là loại việc
        // mà XML doc của chính nó đã dành chỗ sẵn: nghiệp vụ cần nhiều hơn một lượt ghi
        // xuống DB mà vẫn phải nguyên tử.
        //
        // NextNumberAsync chạy `UPDATE … OUTPUT` giữ X lock trên hàng bộ đếm tới hết
        // transaction. Nhờ đó hai người tạo task cùng lúc thì người thứ hai CHỜ một nhịp
        // rồi nhận số kế tiếp, thay vì cả hai cùng đọc một giá trị rồi đụng unique index.
        // Nằm ngoài transaction thì lock nhả ngay sau câu lệnh và bảo đảm biến mất.
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            task.AssignNumber(await _uow.ProjectTaskCounters.NextNumberAsync(request.ProjectId, ct));

            await _uow.Tasks.AddAsync(task, ct);

            _activityLog.Log(nameof(TaskItem), task.Id, ActivityAction.Created,
                task.IsSubtask
                    ? $"Tạo subtask '{task.Name}' của task {task.ParentTaskId}"
                    : $"Tạo task '{task.Name}' (độ ưu tiên {task.Priority})");

            await _uow.SaveChangesAsync(ct);
        }, ct);

        var projectKey = await RequireProjectKeyAsync(request.ProjectId, ct);

        _logger.LogInformation("Tạo task {TaskCode} ({TaskId}) trong project {ProjectId} bởi {EmployeeId}",
            TaskMapper.FormatCode(projectKey, task.Number), task.Id, task.ProjectId, _currentUser.EmployeeId);

        return _mapper.ToSummary(task, projectKey);
    }

    /// <summary>
    /// Chuỗi rỗng/toàn khoảng trắng lưu thành <c>null</c>, không lưu <c>""</c>: hai giá trị
    /// cùng nghĩa "chưa có mô tả" mà khác biểu diễn thì frontend phải kiểm cả hai.
    /// </summary>
    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Việc của người đang đăng nhập, gom theo dự án (ADR-053).
    ///
    /// <para>
    /// 🔑 <b>Không gọi <c>_authz</c>.</b> Đây là endpoint duy nhất không có project trong
    /// URL, nên không có gì để phân quyền theo project — và cũng không cần: bộ lọc đã là
    /// "task được gán cho CHÍNH người gọi", mà muốn được gán thì phải là thành viên đang
    /// hoạt động của dự án đó. Quyền nằm trong chính điều kiện truy vấn, không phải trong
    /// một lượt kiểm thêm.
    /// </para>
    /// </summary>
    public async Task<MyWorkResponse> GetMyWorkAsync(CancellationToken ct = default)
    {
        var employeeId = _currentUser.RequireEmployeeId();
        var today = DateTime.UtcNow.Date;

        var tasks = await _uow.Tasks.GetMyOpenAssignedTasksAsync(employeeId, ct);

        var groups = tasks
            .GroupBy(t => new { t.ProjectId, t.Project.Name, t.Project.Key })
            // Sắp theo TÊN dự án, không theo số task: thứ tự phải ổn định giữa hai lần tải
            // để mắt người dùng nhớ được chỗ, còn số task thì đổi mỗi lần ai đó đóng một việc.
            .OrderBy(g => g.Key.Name)
            .Select(g => new MyWorkGroup(
                g.Key.ProjectId,
                g.Key.Name,
                g.Key.Key,
                g.Select(t => _mapper.ToSummary(t, g.Key.Key)).ToList()))
            .ToList();

        return new MyWorkResponse(
            today,
            tasks.Count,
            tasks.Count(t => t.IsOverdue),
            groups);
    }

    public async Task<TaskDetailResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var task = await _uow.Tasks.GetWithDetailsAsync(id, ct)
            ?? throw new NotFoundException(nameof(TaskItem), id);

        await _authz.AuthorizeTaskAsync(task, ProjectAction.View, ct);

        // Project đã được GetWithDetailsAsync Include sẵn -> không tốn thêm round-trip.
        return _mapper.ToDetail(task, task.Project.Key, _currentUser.RequireEmployeeId());
    }

    public async Task<PagedResult<TaskSummaryResponse>> GetByProjectAsync(
        Guid projectId, PagedRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);

        var paged = await _uow.Tasks.GetPagedByProjectAsync(projectId, request, ct);
        var projectKey = await RequireProjectKeyAsync(projectId, ct);

        return paged.Map(t => _mapper.ToSummary(t, projectKey));
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
        task.Description = Normalize(request.Description);
        task.DueDate = request.DueDate;
        task.Priority = request.Priority;

        _activityLog.Log(nameof(TaskItem), id, ActivityAction.Updated,
            $"Cập nhật task '{task.Name}'");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Cập nhật task {TaskId} bởi {EmployeeId}",
            id, _currentUser.EmployeeId);

        return _mapper.ToDetail(task, task.Project.Key, _currentUser.RequireEmployeeId());
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var task = await _uow.Tasks.GetWithSubtasksAsync(id, ct)
            ?? throw new NotFoundException(nameof(TaskItem), id);

        await _authz.AuthorizeTaskAsync(task, ProjectAction.DeleteTask, ct);

        // ADR-018: chặn thay vì cascade. Subtask là công việc con cùng cấp chi tiết với
        // task cha (không phải "hạ tầng đi kèm" như quan hệ Project->Task ở ADR-008),
        // nên rủi ro mất dữ liệu có ý nghĩa nếu cascade là cao hơn.
        var activeSubtasks = task.Subtasks.Count(s => s.Category != StatusCategory.Done);
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

        return _mapper.ToSummary(task, await RequireProjectKeyAsync(task.ProjectId, ct));
    }

    public async Task<IReadOnlyList<TaskSummaryResponse>> GetBacklogAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);

        var backlog = await _uow.Tasks.GetBacklogAsync(projectId, ct);
        var projectKey = await RequireProjectKeyAsync(projectId, ct);

        return backlog.Select(t => _mapper.ToSummary(t, projectKey)).ToList();
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

        // Một lượt lấy key cho cả board, không phải mỗi thẻ một lượt (ADR-034).
        var projectKey = await RequireProjectKeyAsync(projectId, ct);

        // Duyệt theo DANH SÁCH CỘT của project chứ không theo GroupBy dữ liệu: board phải
        // luôn có đủ mọi cột kể cả cột rỗng, nếu không frontend Kanban lại phải tự dựng cột
        // thiếu. Trước ADR-052 nguồn là `Enum.GetValues<Status>()`; nay là bảng BoardColumns.
        var boardColumns = await _uow.BoardColumns.ListByProjectAsync(projectId, ct);

        var columns = boardColumns
            .Select(column => new BoardColumnGroup(
                new BoardColumnResponse(
                    column.Id, column.Name, column.Color, column.Order, column.Category,
                    // Số task trong cột TRÊN BOARD ĐANG XEM — không phải tổng của cả project.
                    // Board lọc theo sprint, nên hai con số đó khác nhau và cái người dùng
                    // đang nhìn mới là cái đúng để hiển thị.
                    tasks.Count(t => t.BoardColumnId == column.Id)),
                tasks.Where(t => t.BoardColumnId == column.Id)
                     .Select(t => _mapper.ToSummary(t, projectKey))
                     .ToList()))
            .ToList();

        return new BoardResponse(projectId, sprintId, columns);
    }

    /// <summary>
    /// Mã project để ghép mã task. Ném 404 thay vì trả chuỗi rỗng: mã task rỗng
    /// (<c>"-12"</c>) là một lỗi hiển thị im lặng, còn 404 thì lộ ra ngay.
    /// </summary>
    private async Task<string> RequireProjectKeyAsync(Guid projectId, CancellationToken ct)
        => await _uow.Projects.GetKeyAsync(projectId, ct)
           ?? throw new NotFoundException(nameof(Project), projectId);

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
