using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Features.BoardColumns;

namespace PMS.API.Controllers;

/// <summary>
/// Cấu hình cột board của một project (ADR-052).
///
/// <para>
/// Mọi thao tác GHI đều PM-only qua <c>ProjectAction.ManageBoardColumns</c>; đọc thì mở cho
/// mọi thành viên. Người ngoài project nhận <b>404</b> chứ không phải 403 (ADR-019).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class BoardColumnsController : ControllerBase
{
    private readonly IBoardColumnService _columns;
    public BoardColumnsController(IBoardColumnService columns) => _columns = columns;

    [HttpGet("projects/{projectId:guid}/columns")]
    [ProducesResponseType(typeof(IReadOnlyList<BoardColumnResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BoardColumnResponse>>> List(
        Guid projectId, CancellationToken ct)
        => Ok(await _columns.ListAsync(projectId, ct));

    [HttpPost("projects/{projectId:guid}/columns")]
    [ProducesResponseType(typeof(BoardColumnResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BoardColumnResponse>> Create(
        Guid projectId, CreateBoardColumnRequest req, CancellationToken ct)
    {
        var created = await _columns.CreateAsync(projectId, req, ct);
        return CreatedAtAction(nameof(List), new { projectId }, created);
    }

    [HttpPut("columns/{id:guid}")]
    [ProducesResponseType(typeof(BoardColumnResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BoardColumnResponse>> Update(
        Guid id, UpdateBoardColumnRequest req, CancellationToken ct)
        => Ok(await _columns.UpdateAsync(id, req, ct));

    /// <summary>
    /// Xóa cột.
    ///
    /// <para>
    /// ⚠️ <b>DELETE có thân request</b> — khác thường nhưng cần thiết: cột còn task thì phải
    /// kèm <c>targetColumnId</c>. Đưa nó lên query string sẽ khiến một thao tác phá hủy phụ
    /// thuộc vào chuỗi URL, thứ dễ bị sao chép nhầm và nằm lại trong log máy chủ.
    /// </para>
    /// <para>
    /// <b>400</b> khi cột còn task mà không chọn cột đích · <b>409</b> khi đó là cột cuối
    /// cùng · <b>404</b> khi cột đích không thuộc project.
    /// </para>
    /// </summary>
    [HttpDelete("columns/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid id, DeleteBoardColumnRequest req, CancellationToken ct)
    {
        await _columns.DeleteAsync(id, req, ct);
        return NoContent();
    }

    [HttpPut("projects/{projectId:guid}/columns/order")]
    [ProducesResponseType(typeof(IReadOnlyList<BoardColumnResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BoardColumnResponse>>> Reorder(
        Guid projectId, ReorderBoardColumnsRequest req, CancellationToken ct)
        => Ok(await _columns.ReorderAsync(projectId, req, ct));
}
