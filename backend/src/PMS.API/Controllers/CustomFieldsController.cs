using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Features.CustomFields;

namespace PMS.API.Controllers;

/// <summary>
/// Trường tuỳ biến theo project (ADR-059).
///
/// <para>
/// Route chia làm hai nhóm theo đúng hai mức quyền: <c>/projects/{id}/fields*</c> là LƯỢC ĐỒ
/// (PM), <c>/tasks/{id}/field-values</c> là GIÁ TRỊ (ai sửa được task thì sửa được). Đặt
/// giá trị dưới <c>/projects</c> sẽ làm ranh giới đó mờ đi ngay trên URL.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class CustomFieldsController : ControllerBase
{
    private readonly ICustomFieldService _service;

    public CustomFieldsController(ICustomFieldService service) => _service = service;

    // ---------- lược đồ ----------

    [HttpGet("projects/{projectId:guid}/fields")]
    [ProducesResponseType(typeof(IReadOnlyList<FieldDefinitionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<FieldDefinitionResponse>>> List(
        Guid projectId, CancellationToken ct)
        => Ok(await _service.ListAsync(projectId, ct));

    [HttpPost("projects/{projectId:guid}/fields")]
    [ProducesResponseType(typeof(FieldDefinitionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FieldDefinitionResponse>> Create(
        Guid projectId, [FromBody] CreateFieldDefinitionRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(projectId, request, ct);
        return CreatedAtAction(nameof(List), new { projectId }, created);
    }

    [HttpPut("fields/{id:guid}")]
    [ProducesResponseType(typeof(FieldDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FieldDefinitionResponse>> Update(
        Guid id, [FromBody] UpdateFieldDefinitionRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    /// <summary>
    /// Xoá trường và MỌI giá trị của nó (cascade ở tầng DB). Khác <c>DELETE /columns/{id}</c>
    /// — cái đó bắt chọn cột đích vì task không thể không có cột; ở đây "task không có giá
    /// trị cho trường này" là trạng thái hợp lệ, nên không có thân request.
    /// </summary>
    [HttpDelete("fields/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("projects/{projectId:guid}/fields/order")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reorder(
        Guid projectId, [FromBody] ReorderFieldDefinitionsRequest request, CancellationToken ct)
    {
        await _service.ReorderAsync(projectId, request, ct);
        return NoContent();
    }

    // ---------- giá trị ----------

    /// <summary>
    /// Trả về MỌI trường của project kèm giá trị của task (null nếu chưa điền) — không chỉ
    /// các trường đã có giá trị. Frontend dựng thẳng được form từ phản hồi này.
    /// </summary>
    [HttpGet("tasks/{taskId:guid}/field-values")]
    [ProducesResponseType(typeof(IReadOnlyList<FieldValueResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<FieldValueResponse>>> GetValues(
        Guid taskId, CancellationToken ct)
        => Ok(await _service.GetValuesAsync(taskId, ct));

    /// <summary>
    /// Ghi kiểu PATCH: chỉ những trường có mặt trong thân request bị đụng tới. Gửi tất cả
    /// giá trị null cho một trường = xoá giá trị của trường đó.
    /// </summary>
    [HttpPatch("tasks/{taskId:guid}/field-values")]
    [ProducesResponseType(typeof(IReadOnlyList<FieldValueResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<FieldValueResponse>>> SetValues(
        Guid taskId, [FromBody] SetFieldValuesRequest request, CancellationToken ct)
        => Ok(await _service.SetValuesAsync(taskId, request, ct));
}
