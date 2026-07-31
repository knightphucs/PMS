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

    /// <summary>
    /// Viết bình luận trên task. ProjectManager/Member được viết, Viewer bị chặn 403 (§10).
    /// Sinh thông báo cho assignee + watcher + reporter của task.
    /// </summary>
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

    /// <summary>Bình luận của task, cũ nhất trước. Mọi thành viên project đọc được, kể cả Viewer.</summary>
    [HttpGet("tasks/{taskId:guid}/comments")]
    [ProducesResponseType(typeof(PagedResult<CommentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<CommentResponse>>> GetByTask(
        Guid taskId, [FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _comments.GetByTaskAsync(taskId, request, ct));

    /// <summary>Sửa bình luận — chỉ tác giả, ProjectManager cũng không sửa lời người khác (ADR-026).</summary>
    [HttpPut("comments/{id:guid}")]
    [ProducesResponseType(typeof(CommentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentResponse>> Update(
        Guid id, UpdateCommentRequest req, CancellationToken ct)
        => Ok(await _comments.UpdateAsync(id, req, ct));

    /// <summary>Xóa bình luận — tác giả hoặc ProjectManager; xóa cứng (ADR-026).</summary>
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
