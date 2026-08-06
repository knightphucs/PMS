namespace PMS.Application.Features.Reports;

public interface IReportsService
{
    Task<BacklogInsightResponse> GetBacklogInsightAsync(
        Guid projectId, int dueSoonHorizonDays, CancellationToken ct = default);

    Task<VelocityResponse> GetVelocityAsync(Guid projectId, CancellationToken ct = default);

    Task<TimelineResponse> GetTimelineAsync(Guid projectId, CancellationToken ct = default);
}
