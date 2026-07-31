using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Models;
using PMS.Application.Features.Comments;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _comments;

    public CommentsController(ICommentService comments) => _comments = comments;

    [HttpPost("tasks/{taskId:guid}/comments")]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentResponse>> Create(
        Guid taskId, CreateCommentRequest req, CancellationToken ct)
    {
        var created = await _comments.CreateAsync(taskId, req, ct);
        return Created($"/api/v1/tasks/{taskId}/comments", created);
    }

    [HttpGet("tasks/{taskId:guid}/comments")]
    [ProducesResponseType(typeof(PagedResult<CommentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<CommentResponse>>> GetByTask(
        Guid taskId, [FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _comments.GetByTaskAsync(taskId, request, ct));

    [HttpPut("comments/{id:guid}")]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentResponse>> Update(
        Guid id, UpdateCommentRequest req, CancellationToken ct)
        => Ok(await _comments.UpdateAsync(id, req, ct));

    [HttpDelete("comments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _comments.DeleteAsync(id, ct);
        return NoContent();
    }
}
