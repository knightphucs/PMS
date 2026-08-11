using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Features.WorkItemTypes;

namespace PMS.API.Controllers;

/// <summary>Loại công việc theo project (ADR-060).</summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class WorkItemTypesController : ControllerBase
{
    private readonly IWorkItemTypeService _service;

    public WorkItemTypesController(IWorkItemTypeService service) => _service = service;

    [HttpGet("projects/{projectId:guid}/work-item-types")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkItemTypeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<WorkItemTypeResponse>>> List(
        Guid projectId, CancellationToken ct)
        => Ok(await _service.ListAsync(projectId, ct));

    [HttpPost("projects/{projectId:guid}/work-item-types")]
    [ProducesResponseType(typeof(WorkItemTypeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkItemTypeResponse>> Create(
        Guid projectId, [FromBody] CreateWorkItemTypeRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(projectId, request, ct);
        return CreatedAtAction(nameof(List), new { projectId }, created);
    }

    [HttpPut("work-item-types/{id:guid}")]
    [ProducesResponseType(typeof(WorkItemTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkItemTypeResponse>> Update(
        Guid id, [FromBody] UpdateWorkItemTypeRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    /// <summary>
    /// Xoá loại công việc.
    ///
    /// <para>
    /// ⚠️ <b>DELETE có thân request</b> — loại còn task thì bắt buộc kèm
    /// <c>targetTypeId</c>. Cùng khuôn <c>DELETE /columns/{id}</c> (ADR-052).
    /// </para>
    /// <para>
    /// <b>400</b> còn task mà không chọn đích (thông điệp kèm số task) ·
    /// <b>409</b> đó là loại cuối cùng · <b>404</b> loại đích không thuộc project.
    /// </para>
    /// </summary>
    [HttpDelete("work-item-types/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid id, [FromBody] DeleteWorkItemTypeRequest? request, CancellationToken ct)
    {
        await _service.DeleteAsync(id, request ?? new DeleteWorkItemTypeRequest(), ct);
        return NoContent();
    }

    [HttpPut("projects/{projectId:guid}/work-item-types/order")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reorder(
        Guid projectId, [FromBody] ReorderWorkItemTypesRequest request, CancellationToken ct)
    {
        await _service.ReorderAsync(projectId, request, ct);
        return NoContent();
    }
}
