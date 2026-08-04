using Microsoft.AspNetCore.Authorization;
using PMS.Application.Common.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Features.Labels;

namespace PMS.API.Controllers;

/// <summary>
/// Nhãn toàn cục (ADR-037). Chú ý sự bất đối xứng có chủ đích ở đây: <b>tạo</b> nhãn thì
/// ai đã đăng nhập cũng làm được, nhưng <b>sửa/xóa</b> phải là SystemAdmin — vì xóa một
/// nhãn gỡ chip khỏi board của mọi project, còn thêm một nhãn thì không ảnh hưởng ai.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class LabelsController : ControllerBase
{
    private readonly ILabelService _labels;

    public LabelsController(ILabelService labels) => _labels = labels;

    [HttpGet("labels")]
    [ProducesResponseType(typeof(IReadOnlyList<LabelResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LabelResponse>>> GetAll(CancellationToken ct)
        => Ok(await _labels.GetAllAsync(ct));

    [HttpPost("labels")]
    [ProducesResponseType(typeof(LabelResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LabelResponse>> Create(CreateLabelRequest req, CancellationToken ct)
    {
        var created = await _labels.CreateAsync(req, ct);
        return Created("/api/v1/labels", created);
    }

    [HttpPut("labels/{id:guid}")]
    [Authorize(Policy = SystemPermissions.LabelsManage)]
    [ProducesResponseType(typeof(LabelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LabelResponse>> Update(
        Guid id, UpdateLabelRequest req, CancellationToken ct)
        => Ok(await _labels.UpdateAsync(id, req, ct));

    [HttpDelete("labels/{id:guid}")]
    [Authorize(Policy = SystemPermissions.LabelsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _labels.DeleteAsync(id, ct);
        return NoContent();
    }

    // ---------- Gắn/gỡ nhãn trên task: phạm vi project, PM + Member ----------

    [HttpPost("tasks/{taskId:guid}/labels/{labelId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<LabelResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LabelResponse>>> Attach(
        Guid taskId, Guid labelId, CancellationToken ct)
        => Ok(await _labels.AttachToTaskAsync(taskId, labelId, ct));

    [HttpDelete("tasks/{taskId:guid}/labels/{labelId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<LabelResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LabelResponse>>> Detach(
        Guid taskId, Guid labelId, CancellationToken ct)
        => Ok(await _labels.DetachFromTaskAsync(taskId, labelId, ct));
}
