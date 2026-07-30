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
    private readonly SprintMapper _mapper;
    private readonly ILogger<SprintService> _logger;

    public SprintService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        IActivityLogger activityLog, SprintMapper mapper, ILogger<SprintService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
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
