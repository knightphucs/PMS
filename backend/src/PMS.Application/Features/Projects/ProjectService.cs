using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Projects;

public class ProjectService : IProjectService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly INotificationService _notifications;
    private readonly ProjectMapper _mapper;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser, IActivityLogger activityLog, INotificationService notifications, ProjectMapper mapper, ILogger<ProjectService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _notifications = notifications;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ProjectSummaryResponse> CreateAsync(
        CreateProjectRequest request, CancellationToken ct = default)
    {
        var project = Project.Create(
            request.Name, request.Description, request.ExpectedCompletionDate,
            _currentUser.RequireEmployeeId(),
            await GenerateUniqueKeyAsync(request.Name, ct));

        // Bộ đếm task đi cùng project trong CÙNG một SaveChanges: project không có bộ đếm
        // là project không tạo được task nào (ADR-033). Cùng lý do ADR-007 gộp
        // ProjectMember của người tạo vào đây thay vì mở transaction tường minh.
        await _uow.Projects.AddAsync(project, ct);
        _uow.ProjectTaskCounters.Add(new ProjectTaskCounter { ProjectId = project.Id, NextNumber = 0 });

        _activityLog.Log(nameof(Project), project.Id, ActivityAction.Created,
            $"Tạo project '{project.Name}' ({project.Key})");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Tạo project {ProjectId} ({ProjectKey}) bởi {EmployeeId}",
            project.Id, project.Key, _currentUser.EmployeeId);

        // Người tạo luôn là ProjectManager — Project.Create() tự chèn ProjectMember đó.
        return _mapper.ToSummary(project, RoleInProject.ProjectManager);
    }

    /// <summary>
    /// Mã gốc từ tên, thêm hậu tố số nếu đã có người dùng. Unique index trên
    /// <c>Projects.Key</c> vẫn là chốt chặn cuối cho race giữa hai request đồng thời —
    /// vòng lặp này chỉ để trường hợp thường không phải chạm tới nó.
    /// </summary>
    private async Task<string> GenerateUniqueKeyAsync(string name, CancellationToken ct)
    {
        var baseKey = ProjectKeyGenerator.FromName(name);

        for (var attempt = 1; attempt <= MaxKeyAttempts; attempt++)
        {
            var candidate = ProjectKeyGenerator.WithSuffix(baseKey, attempt);
            if (!await _uow.Projects.KeyExistsAsync(candidate, ct)) return candidate;
        }

        // Hết cách sinh mã đọc được — rơi về mã ngẫu nhiên thay vì ném lỗi vào mặt người
        // dùng, vì họ không kiểm soát được thứ đang đụng độ.
        return $"P{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }

    private const int MaxKeyAttempts = 50;

    public async Task<ProjectDetailResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(id, ProjectAction.View, ct);

        var project = await _uow.Projects.GetWithMembersAsync(id, ct)
            ?? throw new NotFoundException(nameof(Project), id);

        return _mapper.ToDetail(project);
    }

    public async Task<PagedResult<ProjectSummaryResponse>> GetMineAsync(
        PagedRequest request, CancellationToken ct = default)
    {
        var paged = await _uow.Projects.GetPagedForEmployeeAsync(
            _currentUser.RequireEmployeeId(), 
            request, ct);

        return paged.Map(x => _mapper.ToSummary(x.Project, x.RoleInProject));
    }

    public async Task<ProjectDetailResponse> UpdateAsync(
        Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(id, ProjectAction.Update, ct);

        var project = await _uow.Projects.GetWithMembersAsync(id, ct)
            ?? throw new NotFoundException(nameof(Project), id);

        _uow.SetConcurrencyToken(project, request.RowVersion);

        project.Name = request.Name.Trim();
        project.Description = request.Description.Trim();
        project.ExpectedCompletionDate = request.ExpectedCompletionDate;

        _activityLog.Log(nameof(Project), id, ActivityAction.Updated,
            $"Cập nhật thông tin project '{project.Name}'");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Cập nhật project {ProjectId} bởi {EmployeeId}",
            id, _currentUser.EmployeeId);

        return _mapper.ToDetail(project);
    }

    public Task<ProjectDetailResponse> CompleteAsync(Guid id, CancellationToken ct = default)
        => ChangeStatusAsync(id, complete: true, ct);

    public Task<ProjectDetailResponse> ReopenAsync(Guid id, CancellationToken ct = default)
        => ChangeStatusAsync(id, complete: false, ct);

    private async Task<ProjectDetailResponse> ChangeStatusAsync(
        Guid id, bool complete, CancellationToken ct)
    {
        await _authz.AuthorizeAsync(id, ProjectAction.Update, ct);

        var project = await _uow.Projects.GetWithMembersAsync(id, ct)
            ?? throw new NotFoundException(nameof(Project), id);

        var before = project.Status;

        // `Complete()` idempotent, `Reopen()` ném DomainException (-> 409) nếu chưa Done.
        if (complete) project.Complete();
        else project.Reopen();

        if (project.Status == before) return _mapper.ToDetail(project);

        _activityLog.Log(nameof(Project), id, ActivityAction.StatusChanged,
            $"Đổi trạng thái project '{project.Name}': {before} -> {project.Status}");

        // Báo cho MỌI thành viên đang hoạt động, trừ chính người thao tác. Đây là thay đổi
        // ở cấp project nên nó ảnh hưởng tới việc của tất cả, khác các thông báo cấp task
        // vốn chỉ tới người liên quan.
        var actorId = _currentUser.RequireEmployeeId();
        var recipients = project.Members
            .Where(m => m.IsActive() && m.EmployeeId != actorId)
            .Select(m => m.EmployeeId)
            .ToList();

        if (recipients.Count > 0)
            // ⚠️ `ProjectStatusChanged` chứ KHÔNG phải `StatusChanged`: `RelatedEntityKind`
            // được suy ra từ `Type` (ADR-025), và `StatusChanged` suy ra `Task` — dùng nó ở
            // đây sẽ khiến chuông điều hướng tới `/tasks/{projectId}`, một id không tồn tại.
            _notifications.NotifyMany(
                recipients,
                NotificationType.ProjectStatusChanged,
                complete
                    ? $"Dự án '{project.Name}' đã được đánh dấu hoàn thành."
                    : $"Dự án '{project.Name}' đã được mở lại.",
                id);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Đổi trạng thái project {ProjectId}: {Before} -> {After} bởi {EmployeeId}",
            id, before, project.Status, actorId);

        return _mapper.ToDetail(project);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(id, ProjectAction.Delete, ct);

        var project = await _uow.Projects.GetForDeletionAsync(id, ct)
            ?? throw new NotFoundException(nameof(Project), id);

        var activeCount = project.Tasks.Count(t => t.Category != StatusCategory.Done);
        if (activeCount > 0)
            throw new ConflictException(
                $"Không thể xóa project khi còn {activeCount} task chưa hoàn thành. " +
                "Hãy hoàn thành hoặc xóa các task đó trước.");

        foreach (var task in project.Tasks) _uow.Tasks.Remove(task);
        foreach (var sprint in project.Sprints) _uow.Sprints.Remove(sprint);
        _uow.Projects.Remove(project);

        // Ghi log TRƯỚC SaveChanges để cùng một transaction với thay đổi nghiệp vụ (ADR-013).
        // Bản thân ActivityLog không bị xóa theo — đó chính là lý do project dùng xóa mềm.
        _activityLog.Log(nameof(Project), id, ActivityAction.Deleted,
            $"Xóa project '{project.Name}' cùng {project.Tasks.Count} task và {project.Sprints.Count} sprint");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Xóa mềm project {ProjectId} bởi {EmployeeId}: {TaskCount} task, {SprintCount} sprint",
            id, _currentUser.EmployeeId, project.Tasks.Count, project.Sprints.Count);
    }
}