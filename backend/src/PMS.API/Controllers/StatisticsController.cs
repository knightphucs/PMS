using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Features.Statistics;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class StatisticsController : ControllerBase
{
    private readonly IStatisticsService _statistics;

    public StatisticsController(IStatisticsService statistics) => _statistics = statistics;

    [HttpGet("projects/{projectId:guid}/statistics")]
    [ProducesResponseType(typeof(ProjectStatisticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectStatisticsResponse>> GetProjectStatistics(
        Guid projectId, CancellationToken ct)
        => Ok(await _statistics.GetProjectStatisticsAsync(projectId, ct));
}
