using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Features.TaskLinks;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class TaskLinksController : ControllerBase
{
    private readonly ITaskLinkService _links;

    public TaskLinksController(ITaskLinkService links) => _links = links;

    [HttpGet("tasks/{taskId:guid}/links")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskLinkResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TaskLinkResponse>>> GetByTask(
        Guid taskId, CancellationToken ct)
        => Ok(await _links.GetByTaskAsync(taskId, ct));

    [HttpPost("tasks/{taskId:guid}/links")]
    [ProducesResponseType(typeof(TaskLinkResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskLinkResponse>> Create(
        Guid taskId, CreateTaskLinkRequest req, CancellationToken ct)
    {
        var created = await _links.CreateAsync(taskId, req, ct);
        return Created($"/api/v1/tasks/{taskId}/links", created);
    }

    // Route theo id của LIÊN KẾT, không lồng dưới task: một liên kết thuộc về hai task nên
    // lồng dưới một trong hai sẽ ngụ ý một quyền sở hữu không có thật.
    [HttpDelete("task-links/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _links.DeleteAsync(id, ct);
        return NoContent();
    }
}
