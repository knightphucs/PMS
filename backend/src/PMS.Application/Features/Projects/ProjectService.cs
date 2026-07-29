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
    private readonly ProjectMapper _mapper;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser, ProjectMapper mapper, ILogger<ProjectService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ProjectSummaryResponse> CreateAsync(
        CreateProjectRequest request, CancellationToken ct = default)
    {
        var project = Project.Create(
            request.Name, request.Description, request.ExpectedCompletionDate,
            _currentUser.RequireEmployeeId());
        await _uow.Projects.AddAsync(project, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Tạo project {ProjectId} bởi {EmployeeId}",
            project.Id, _currentUser.EmployeeId);

        return _mapper.ToSummary(project);
    }

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

        return paged.Map(_mapper.ToSummary);
    }

    public async Task<ProjectDetailResponse> UpdateAsync(
        Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(id, ProjectAction.Update, ct);

        var project = await _uow.Projects.GetWithMembersAsync(id, ct)
            ?? throw new NotFoundException(nameof(Project), id);

        project.Name = request.Name.Trim();
        project.Description = request.Description.Trim();
        project.ExpectedCompletionDate = request.ExpectedCompletionDate;

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Cập nhật project {ProjectId} bởi {EmployeeId}",
            id, _currentUser.EmployeeId);

        return _mapper.ToDetail(project);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(id, ProjectAction.Delete, ct);

        var project = await _uow.Projects.GetForDeletionAsync(id, ct)
            ?? throw new NotFoundException(nameof(Project), id);

        var activeCount = project.Tasks.Count(t => t.Status != Status.Done);
        if (activeCount > 0)
            throw new ConflictException(
                $"Không thể xóa project khi còn {activeCount} task chưa hoàn thành. " +
                "Hãy hoàn thành hoặc xóa các task đó trước.");

        foreach (var task in project.Tasks) _uow.Tasks.Remove(task);
        foreach (var sprint in project.Sprints) _uow.Sprints.Remove(sprint);
        _uow.Projects.Remove(project);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Xóa mềm project {ProjectId} bởi {EmployeeId}: {TaskCount} task, {SprintCount} sprint",
            id, _currentUser.EmployeeId, project.Tasks.Count, project.Sprints.Count);
    }
}