using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Sprints;

public class SprintService : ISprintService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly INotificationService _notifications;
    private readonly SprintMapper _mapper;
    private readonly ILogger<SprintService> _logger;

    public SprintService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        IActivityLogger activityLog, INotificationService notifications,
        SprintMapper mapper, ILogger<SprintService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _notifications = notifications;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SprintResponse> CreateAsync(
        Guid projectId, CreateSprintRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.ManageSprint, ct);

        var sprint = new Sprint
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = request.Name.Trim(),
            Goal = request.Goal.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        await _uow.Sprints.AddAsync(sprint, ct);

        _activityLog.Log(nameof(Project), projectId, ActivityAction.Created,
            $"Tạo sprint '{sprint.Name}' ({sprint.StartDate:dd/MM/yyyy} - {sprint.EndDate:dd/MM/yyyy})");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Tạo sprint {SprintId} trong project {ProjectId} bởi {EmployeeId}",
            sprint.Id, projectId, _currentUser.EmployeeId);

        return _mapper.ToResponse(sprint);
    }

    public async Task<IReadOnlyList<SprintResponse>> GetByProjectAsync(
        Guid projectId, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);

        var sprints = await _uow.Sprints.GetByProjectAsync(projectId, ct);

        return sprints.Select(_mapper.ToResponse).ToList();
    }

    public async Task<SprintResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var sprint = await LoadAndAuthorizeAsync(id, ProjectAction.View, ct);

        return _mapper.ToResponse(sprint);
    }

    public async Task<SprintResponse> UpdateAsync(
        Guid id, UpdateSprintRequest request, CancellationToken ct = default)
    {
        var sprint = await LoadAndAuthorizeAsync(id, ProjectAction.ManageSprint, ct);

        sprint.Name = request.Name.Trim();
        sprint.Goal = request.Goal.Trim();
        sprint.StartDate = request.StartDate;
        sprint.EndDate = request.EndDate;

        _activityLog.Log(nameof(Project), sprint.ProjectId, ActivityAction.Updated,
            $"Cập nhật sprint '{sprint.Name}'");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Cập nhật sprint {SprintId} bởi {EmployeeId}",
            id, _currentUser.EmployeeId);

        return _mapper.ToResponse(sprint);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var sprint = await LoadAndAuthorizeAsync(id, ProjectAction.ManageSprint, ct);

        // ADR-020: đẩy task về Backlog thay vì chặn hay cascade. Bắt buộc phải null hóa
        // SprintId — Sprint là ISoftDeletable nên nếu để task trỏ tới sprint đã xóa mềm,
        // Include(t => t.Sprint) sẽ trả null một cách khó hiểu. FK cũng là Restrict
        // (SprintConfiguration) nên xóa cứng sẽ nổ ở tầng DB.
        var movedToBacklog = sprint.Tasks.Count;
        foreach (var task in sprint.Tasks) task.SprintId = null;

        _uow.Sprints.Remove(sprint);

        _activityLog.Log(nameof(Project), sprint.ProjectId, ActivityAction.Deleted,
            $"Xóa sprint '{sprint.Name}', chuyển {movedToBacklog} task về Backlog");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Xóa mềm sprint {SprintId} bởi {EmployeeId}: {TaskCount} task về Backlog",
            id, _currentUser.EmployeeId, movedToBacklog);
    }

    public async Task<SprintResponse> StartAsync(Guid id, CancellationToken ct = default)
    {
        var sprint = await LoadAndAuthorizeAsync(id, ProjectAction.ManageSprint, ct);

        // Một project chỉ có tối đa MỘT sprint đang chạy. Không phải luật của Scrum cho vui:
        // "sprint hiện tại" là khái niệm mà backlog, board mặc định và velocity đều dựa vào,
        // và hai sprint cùng Active thì cả ba đều không trả lời được câu hỏi đó.
        var running = await _uow.Sprints.GetActiveOfProjectAsync(sprint.ProjectId, ct);
        if (running is not null && running.Id != sprint.Id)
            throw new ConflictException(
                $"Project đang chạy sprint '{running.Name}'. Hãy đóng sprint đó trước.");

        sprint.Start();

        _activityLog.Log(nameof(Project), sprint.ProjectId, ActivityAction.Updated,
            $"Bắt đầu sprint '{sprint.Name}'");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Bắt đầu sprint {SprintId} bởi {EmployeeId}",
            id, _currentUser.EmployeeId);

        return _mapper.ToResponse(sprint);
    }

    public async Task<SprintCompletionPreview> PreviewCompletionAsync(
        Guid id, CancellationToken ct = default)
    {
        // View chứ không ManageSprint: đây là màn XEM TRƯỚC, ai đọc được sprint thì xem được.
        var sprint = await LoadAndAuthorizeAsync(id, ProjectAction.View, ct);

        var done = sprint.Tasks.Count(t => t.Category == StatusCategory.Done);

        var siblings = await _uow.Sprints.GetByProjectAsync(sprint.ProjectId, ct);

        return new SprintCompletionPreview(
            sprint.Id,
            sprint.Name,
            done,
            sprint.Tasks.Count - done,
            // Chỉ sprint CHƯA đóng và khác chính nó mới là đích hợp lệ — đẩy task chưa xong
            // vào một sprint đã chốt sổ là làm hỏng chính con số mà việc chốt sổ tạo ra.
            siblings
                .Where(s => s.Id != sprint.Id && s.Status != SprintStatus.Completed)
                .OrderBy(s => s.StartDate)
                .Select(s => new SprintOption(s.Id, s.Name, s.StartDate, s.EndDate))
                .ToList());
    }

    public async Task<SprintResponse> CompleteAsync(
        Guid id, CompleteSprintRequest request, CancellationToken ct = default)
    {
        var sprint = await LoadAndAuthorizeAsync(id, ProjectAction.ManageSprint, ct);

        var unfinished = sprint.Tasks
            .Where(t => t.Category != StatusCategory.Done)
            .ToList();

        Sprint? target = null;

        if (unfinished.Count > 0 && request.TargetSprintId is { } targetId)
        {
            target = await _uow.Sprints.GetWithTasksAsync(targetId, ct);

            // 404 chứ không 400: sprint của project khác thì với người gọi nó không tồn tại.
            if (target is null || target.ProjectId != sprint.ProjectId)
                throw new NotFoundException(nameof(Sprint), targetId);

            if (target.Id == sprint.Id)
                throw new BusinessRuleException("Không thể chuyển task sang chính sprint đang đóng.");

            if (target.Status == SprintStatus.Completed)
                throw new BusinessRuleException(
                    $"Sprint '{target.Name}' đã đóng — không nhận thêm task được.");
        }

        // ADR-050: `TargetSprintId = null` nghĩa là ĐẨY VỀ BACKLOG, một lựa chọn hợp lệ chứ
        // không phải "chưa chọn". Việc phân biệt hai thứ đó nằm ở hợp đồng DTO, và ở đây thì
        // cả hai nhánh đều là một phép gán duy nhất.
        foreach (var task in unfinished) task.SprintId = target?.Id;

        sprint.Complete(DateTime.UtcNow);

        var destination = target is null ? "Backlog" : $"sprint '{target.Name}'";

        _activityLog.Log(nameof(Project), sprint.ProjectId, ActivityAction.Updated,
            unfinished.Count > 0
                ? $"Đóng sprint '{sprint.Name}', chuyển {unfinished.Count} task chưa xong sang {destination}"
                : $"Đóng sprint '{sprint.Name}'");

        // Báo cho cả đội: đóng sprint là mốc mà mọi người cần biết, và task của họ vừa bị
        // chuyển chỗ mà không phải do họ làm.
        _notifications.NotifyMany(
            sprint.Tasks.SelectMany(t => t.Assignments.Select(a => a.EmployeeId)).Distinct(),
            NotificationType.SprintCompleted,
            $"Sprint '{sprint.Name}' đã đóng. {unfinished.Count} task chưa xong chuyển sang {destination}.",
            sprint.ProjectId);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Đóng sprint {SprintId} bởi {EmployeeId}: {Unfinished} task chưa xong -> {Destination}",
            id, _currentUser.EmployeeId, unfinished.Count, destination);

        return _mapper.ToResponse(sprint);
    }

    /// <summary>
    /// Nạp sprint rồi kiểm quyền theo project chứa nó. Chuẩn hóa 404 (ADR-019): sprint không
    /// tồn tại và sprint thuộc project mình không phải thành viên đều trả cùng một thông báo,
    /// không để lộ sự tồn tại của sprint cho người ngoài.
    /// </summary>
    private async Task<Sprint> LoadAndAuthorizeAsync(
        Guid sprintId, ProjectAction action, CancellationToken ct)
    {
        var sprint = await _uow.Sprints.GetWithTasksAsync(sprintId, ct)
            ?? throw new NotFoundException(nameof(Sprint), sprintId);

        try
        {
            await _authz.AuthorizeAsync(sprint.ProjectId, action, ct);
        }
        catch (NotFoundException)
        {
            throw new NotFoundException(nameof(Sprint), sprintId);
        }

        return sprint;
    }
}
