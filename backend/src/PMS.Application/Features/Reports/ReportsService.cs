using PMS.Application.Common;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Reports;

/// <summary>
/// Nhóm báo cáo kiểu Jira (§1 hạng mục 11 ARCHITECTURE.md) — backlog insight + velocity +
/// timeline.
///
/// <para>
/// Dùng chung <see cref="ProjectAction.ViewStatistics"/> với <c>StatisticsService</c> —
/// không tạo action mới: báo cáo là cùng một tầng quyền "ai xem được project thì xem được
/// số liệu tổng hợp của nó" (ADR-039, cả ba vai trò).
/// </para>
/// </summary>
public class ReportsService : IReportsService
{
    private readonly IProjectStatisticsRepository _stats;
    private readonly IProjectAuthorizationService _authz;

    public ReportsService(IProjectStatisticsRepository stats, IProjectAuthorizationService authz)
    {
        _stats = stats;
        _authz = authz;
    }

    public async Task<BacklogInsightResponse> GetBacklogInsightAsync(
        Guid projectId, int dueSoonHorizonDays, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.ViewStatistics, ct);

        if (dueSoonHorizonDays <= 0)
            throw new BusinessRuleException("Số ngày \"sắp đến hạn\" phải là số dương.");

        var insight = await _stats.GetBacklogInsightAsync(projectId, dueSoonHorizonDays, ct);
        var byPriority = await _stats.GetBacklogByPriorityAsync(projectId, ct);

        return new BacklogInsightResponse(
            projectId, insight.TotalOpen, insight.Overdue, insight.DueSoon, insight.NoDueDate,
            EnumZeroFill.Fill(byPriority, t => t.Priority, t => t.Count,
                (priority, count) => new Statistics.PriorityCount(priority, count)));
    }

    public async Task<VelocityResponse> GetVelocityAsync(Guid projectId, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.ViewStatistics, ct);

        var tallies = await _stats.TallyVelocityAsync(projectId, ct);

        var points = tallies
            .Select(t => new SprintVelocityPoint(t.SprintId, t.Name, t.CompletedAt, t.Done, t.Total))
            .ToList();

        // Không chia cho 0: dự án chưa đóng sprint nào thì "tốc độ trung bình" không có
        // nghĩa, và 0 đọc đúng hơn là NaN/exception cho một biểu đồ rỗng.
        var average = points.Count == 0
            ? 0m
            : Math.Round((decimal)points.Sum(p => p.DoneCount) / points.Count, 2);

        return new VelocityResponse(projectId, points, average);
    }

    public async Task<TimelineResponse> GetTimelineAsync(Guid projectId, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.ViewStatistics, ct);

        var tallies = await _stats.TallyTimelineAsync(projectId, ct);
        var today = DateTime.UtcNow.Date;

        var points = tallies
            .Select(t => new SprintTimelinePoint(
                t.SprintId, t.Name, t.Status, t.StartDate, t.EndDate, t.CompletedAt, t.Total, t.Done,
                // Cùng công thức Sprint.IsOverdue — không gọi được property đó vì tally
                // không phải entity Sprint đã nạp.
                t.Status == SprintStatus.Active && today > t.EndDate.Date))
            .ToList();

        return new TimelineResponse(projectId, points);
    }
}
