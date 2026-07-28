using PMS.Application.Common.Models;

namespace PMS.Application.Features.Projects;

public interface IProjectService
{
    Task<ProjectSummaryResponse> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);

    Task<ProjectDetailResponse> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<ProjectSummaryResponse>> GetMineAsync(PagedRequest request, CancellationToken ct = default);

    Task<ProjectDetailResponse> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}