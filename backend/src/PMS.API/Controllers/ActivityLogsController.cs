using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Models;
using PMS.Application.Features.ActivityLogs;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class ActivityLogsController : ControllerBase
{
    private readonly IActivityLogService _activity;

    public ActivityLogsController(IActivityLogService activity) => _activity = activity;

    [HttpGet("tasks/{taskId:guid}/activity")]
    [ProducesResponseType(typeof(PagedResult<ActivityLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<ActivityLogResponse>>> GetTaskActivity(
        Guid taskId, [FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _activity.GetTaskActivityAsync(taskId, request, ct));

    [HttpGet("projects/{projectId:guid}/activity")]
    [ProducesResponseType(typeof(PagedResult<ActivityLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<ActivityLogResponse>>> GetProjectActivity(
        Guid projectId, [FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _activity.GetProjectActivityAsync(projectId, request, ct));
}
