namespace PMS.Application.Features.Statistics;

public interface IStatisticsService
{
    Task<ProjectStatisticsResponse> GetProjectStatisticsAsync(
        Guid projectId, CancellationToken ct = default);
}
