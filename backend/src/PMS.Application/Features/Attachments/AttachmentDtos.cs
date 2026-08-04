namespace PMS.Application.Features.Attachments;

/// <summary>
/// Đầu vào của service. Cố ý KHÔNG dùng <c>IFormFile</c>: đó là kiểu của ASP.NET Core, và
/// <c>PMS.Application</c> không được phụ thuộc hạ tầng web (§3 — dependency đi vào trong).
/// Controller chịu trách nhiệm chuyển đổi.
/// </summary>
public record UploadAttachmentRequest(
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content);

public record AttachmentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid UploaderId,
    string UploaderName,
    Guid? TaskId,
    Guid? ProjectId,
    DateTime CreatedAt);

/// <summary>Nội dung file để controller trả về — <c>ContentType</c> KHÔNG nằm ở đây, xem AttachmentsController.</summary>
public record AttachmentDownload(Stream Content, string FileName);
