using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Features.Sprints;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class SprintsController : ControllerBase
{
    private readonly ISprintService _sprints;
    public SprintsController(ISprintService sprints) => _sprints = sprints;

    [HttpPost("projects/{projectId:guid}/sprints")]
    [ProducesResponseType(typeof(SprintResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintResponse>> Create(
        Guid projectId, CreateSprintRequest req, CancellationToken ct)
    {
        var created = await _sprints.CreateAsync(projectId, req, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("projects/{projectId:guid}/sprints")]
    [ProducesResponseType(typeof(IReadOnlyList<SprintResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SprintResponse>>> GetByProject(
        Guid projectId, CancellationToken ct)
        => Ok(await _sprints.GetByProjectAsync(projectId, ct));

    [HttpGet("sprints/{id:guid}")]
    [ProducesResponseType(typeof(SprintResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _sprints.GetByIdAsync(id, ct));

    [HttpPut("sprints/{id:guid}")]
    [ProducesResponseType(typeof(SprintResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintResponse>> Update(
        Guid id, UpdateSprintRequest req, CancellationToken ct)
        => Ok(await _sprints.UpdateAsync(id, req, ct));

    /// <summary>Xóa sprint; task của sprint được chuyển về Backlog chứ không bị xóa (ADR-020).</summary>
    [HttpDelete("sprints/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sprints.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Bắt đầu sprint (ADR-050). Idempotent — gọi lại trên sprint đang chạy trả 200.
    /// <b>409</b> khi project đã có sprint khác đang chạy, hoặc sprint này đã đóng.
    /// </summary>
    [HttpPost("sprints/{id:guid}/start")]
    [ProducesResponseType(typeof(SprintResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SprintResponse>> Start(Guid id, CancellationToken ct)
        => Ok(await _sprints.StartAsync(id, ct));

    /// <summary>
    /// Xem trước việc đóng sprint — số task chưa xong và danh sách sprint đích hợp lệ.
    /// Frontend gọi cái này để dựng dialog đóng sprint (ADR-050).
    /// </summary>
    [HttpGet("sprints/{id:guid}/completion-preview")]
    [ProducesResponseType(typeof(SprintCompletionPreview), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintCompletionPreview>> PreviewCompletion(
        Guid id, CancellationToken ct)
        => Ok(await _sprints.PreviewCompletionAsync(id, ct));

    /// <summary>
    /// Đóng sprint (ADR-050) — <b>hỏi task chưa xong đi đâu, không tự quyết hộ</b>.
    ///
    /// <para>
    /// <c>targetSprintId = null</c> nghĩa là đẩy về Backlog, và đó là một lựa chọn hợp lệ
    /// chứ không phải "chưa chọn".
    /// </para>
    /// <para>
    /// <b>409</b> khi sprint chưa bắt đầu hoặc đã đóng · <b>400</b> khi sprint đích đã đóng
    /// hoặc trùng chính nó · <b>404</b> khi sprint đích thuộc project khác.
    /// </para>
    /// </summary>
    [HttpPost("sprints/{id:guid}/complete")]
    [ProducesResponseType(typeof(SprintResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SprintResponse>> Complete(
        Guid id, CompleteSprintRequest req, CancellationToken ct)
        => Ok(await _sprints.CompleteAsync(id, req, ct));
}
