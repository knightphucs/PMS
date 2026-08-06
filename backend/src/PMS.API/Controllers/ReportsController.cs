using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Features.Reports;

namespace PMS.API.Controllers;

/// <summary>
/// Nhóm báo cáo kiểu Jira (§1 hạng mục 11 ARCHITECTURE.md) — backlog insight + velocity +
/// timeline. Soi gương <c>StatisticsController</c>: cùng quyền (mọi thành viên xem được,
/// ADR-039), người ngoài project nhận 404 chứ không phải 403 (ADR-006/019).
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportsService _reports;
    public ReportsController(IReportsService reports) => _reports = reports;

    /// <summary><paramref name="dueSoonHorizonDays"/> mặc định 7 — "sắp đến hạn trong tuần tới".</summary>
    [HttpGet("projects/{projectId:guid}/reports/backlog-insight")]
    [ProducesResponseType(typeof(BacklogInsightResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BacklogInsightResponse>> GetBacklogInsight(
        Guid projectId, [FromQuery] int? dueSoonHorizonDays, CancellationToken ct)
        => Ok(await _reports.GetBacklogInsightAsync(projectId, dueSoonHorizonDays ?? 7, ct));

    [HttpGet("projects/{projectId:guid}/reports/velocity")]
    [ProducesResponseType(typeof(VelocityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VelocityResponse>> GetVelocity(Guid projectId, CancellationToken ct)
        => Ok(await _reports.GetVelocityAsync(projectId, ct));

    /// <summary>Mọi sprint (Planned/Active/Completed), sắp theo ngày bắt đầu — roadmap kiểu Jira.</summary>
    [HttpGet("projects/{projectId:guid}/reports/timeline")]
    [ProducesResponseType(typeof(TimelineResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TimelineResponse>> GetTimeline(Guid projectId, CancellationToken ct)
        => Ok(await _reports.GetTimelineAsync(projectId, ct));
}
