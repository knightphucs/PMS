using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Features.Watchers;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class WatchersController : ControllerBase
{
    private readonly IWatcherService _watchers;

    public WatchersController(IWatcherService watchers) => _watchers = watchers;

    [HttpGet("tasks/{taskId:guid}/watchers")]
    [ProducesResponseType(typeof(IReadOnlyList<WatcherResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<WatcherResponse>>> GetByTask(
        Guid taskId, CancellationToken ct)
        => Ok(await _watchers.GetByTaskAsync(taskId, ct));

    // `/me` chứ không nhận employeeId: chỉ tự theo dõi cho MÌNH, không ai ép người khác
    // theo dõi được. Ràng buộc nằm ở hình dạng route, không phải ở một dòng kiểm tra.
    [HttpPost("tasks/{taskId:guid}/watchers/me")]
    [ProducesResponseType(typeof(WatchStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WatchStateResponse>> Watch(Guid taskId, CancellationToken ct)
        => Ok(await _watchers.WatchAsync(taskId, ct));

    [HttpDelete("tasks/{taskId:guid}/watchers/me")]
    [ProducesResponseType(typeof(WatchStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WatchStateResponse>> Unwatch(Guid taskId, CancellationToken ct)
        => Ok(await _watchers.UnwatchAsync(taskId, ct));
}
