using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Exceptions;
using PMS.Application.Features.Attachments;

namespace PMS.API.Controllers;

/// <summary>
/// File đính kèm cho Task và Project (ADR-035). Subtask không cần route riêng — subtask là
/// một <c>TaskItem</c> đầy đủ nên dùng chung endpoint task.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _attachments;

    public AttachmentsController(IAttachmentService attachments) => _attachments = attachments;

    [HttpPost("tasks/{taskId:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AttachmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<AttachmentResponse>> UploadToTask(
        Guid taskId, IFormFile file, CancellationToken ct)
    {
        var created = await _attachments.UploadToTaskAsync(taskId, ToRequest(file), ct);
        return Created($"/api/v1/attachments/{created.Id}", created);
    }

    [HttpPost("projects/{projectId:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(AttachmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<AttachmentResponse>> UploadToProject(
        Guid projectId, IFormFile file, CancellationToken ct)
    {
        var created = await _attachments.UploadToProjectAsync(projectId, ToRequest(file), ct);
        return Created($"/api/v1/attachments/{created.Id}", created);
    }

    [HttpGet("tasks/{taskId:guid}/attachments")]
    [ProducesResponseType(typeof(IReadOnlyList<AttachmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AttachmentResponse>>> GetByTask(
        Guid taskId, CancellationToken ct)
        => Ok(await _attachments.GetByTaskAsync(taskId, ct));

    [HttpGet("projects/{projectId:guid}/attachments")]
    [ProducesResponseType(typeof(IReadOnlyList<AttachmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AttachmentResponse>>> GetByProject(
        Guid projectId, CancellationToken ct)
        => Ok(await _attachments.GetByProjectAsync(projectId, ct));

    [HttpGet("attachments/{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var download = await _attachments.DownloadAsync(id, ct);

        // 🔴 Ba thứ dưới đây đi CÙNG NHAU, không được tháo cái nào:
        //   • nosniff        — cấm trình duyệt tự đoán kiểu nội dung
        //   • octet-stream   — KHÔNG trả ContentType đã lưu, dù ta có nó trong DB
        //   • File(..., fileDownloadName) — sinh Content-Disposition: attachment
        // Cộng lại, chúng triệt mọi đường render inline một payload HTML/SVG ngay trên
        // origin của API. Cái giá đã chấp nhận: muốn xem trước ảnh inline thì phải làm một
        // endpoint riêng chỉ nhận ảnh — không nằm trong phạm vi phiên này (ADR-035).
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(download.Content, "application/octet-stream", download.FileName);
    }

    [HttpDelete("attachments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _attachments.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Chuyển <c>IFormFile</c> (kiểu của ASP.NET Core) sang DTO thuần để tầng Application
    /// không phải phụ thuộc hạ tầng web.
    /// <para>
    /// ⚠️ <c>ValidationFilter</c> KHÔNG chạy cho action multipart — nó tra
    /// <c>IValidator&lt;IFormFile&gt;</c> và không tìm thấy gì. Mọi kiểm tra nằm trong
    /// <c>AttachmentContentValidator</c>, gọi từ service. Đừng thêm validator FluentValidation
    /// ở đây và tưởng rằng nó có tác dụng.
    /// </para>
    /// </summary>
    private static UploadAttachmentRequest ToRequest(IFormFile? file)
    {
        if (file is null)
            throw new BusinessRuleException("Thiếu file — gửi bằng multipart/form-data, trường 'file'.");

        return new UploadAttachmentRequest(
            file.FileName, file.ContentType, file.Length, file.OpenReadStream());
    }
}
