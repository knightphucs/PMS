using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Models;
using PMS.Application.Features.SavedViews;

namespace PMS.API.Controllers;

/// <summary>
/// View lưu được trên danh sách task (ADR-061).
///
/// <para>
/// Route chia hai nhóm giống <c>CustomFieldsController</c>: <c>/projects/{id}/views*</c> là
/// KHAI BÁO view, còn <c>/projects/{id}/tasks/query</c> là CHẠY một bộ lọc.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class SavedViewsController : ControllerBase
{
    private readonly ISavedViewService _service;

    public SavedViewsController(ISavedViewService service) => _service = service;

    // ---------- khai báo ----------

    /// <summary>View chia sẻ của dự án + view riêng của chính người gọi.</summary>
    [HttpGet("projects/{projectId:guid}/views")]
    [ProducesResponseType(typeof(IReadOnlyList<SavedViewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SavedViewResponse>>> List(
        Guid projectId, CancellationToken ct)
        => Ok(await _service.ListAsync(projectId, ct));

    [HttpPost("projects/{projectId:guid}/views")]
    [ProducesResponseType(typeof(SavedViewResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SavedViewResponse>> Create(
        Guid projectId, [FromBody] CreateSavedViewRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(projectId, request, ct);
        return CreatedAtAction(nameof(List), new { projectId }, created);
    }

    [HttpPut("views/{id:guid}")]
    [ProducesResponseType(typeof(SavedViewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SavedViewResponse>> Update(
        Guid id, [FromBody] UpdateSavedViewRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    [HttpDelete("views/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("projects/{projectId:guid}/views/order")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reorder(
        Guid projectId, [FromBody] ReorderSavedViewsRequest request, CancellationToken ct)
    {
        await _service.ReorderAsync(projectId, request, ct);
        return NoContent();
    }

    // ---------- chạy ----------

    /// <summary>
    /// Chạy một bộ lọc trên danh sách task của dự án.
    ///
    /// <para>
    /// 🔴 <b>POST cho một thao tác ĐỌC — có chủ đích.</b> Bộ lọc là một danh sách đối tượng
    /// (trường, toán tử, giá trị) × tối đa 20 dòng; nhồi nó vào query string sẽ cần một cú
    /// pháp mã hoá tự chế mà cả hai đầu phải cùng hiểu — tức đúng loại "hai nơi cùng dựng
    /// một thứ" mà ADR-034 đã đặt tên. Thân request JSON thì hợp đồng nằm ở một chỗ.
    /// </para>
    /// <para>
    /// Không nhận <c>viewId</c>: client gửi thẳng trạng thái view đang mở, nên "xem thử
    /// trước khi lưu" dùng chung đúng đường này — xem <see cref="TaskQueryRequest"/>.
    /// </para>
    /// </summary>
    [HttpPost("projects/{projectId:guid}/tasks/query")]
    [ProducesResponseType(typeof(PagedResult<TaskListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TaskListItemResponse>>> QueryTasks(
        Guid projectId, [FromBody] TaskQueryRequest request, CancellationToken ct)
        => Ok(await _service.QueryTasksAsync(projectId, request, ct));
}
