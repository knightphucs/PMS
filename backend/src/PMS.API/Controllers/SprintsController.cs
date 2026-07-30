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
}
