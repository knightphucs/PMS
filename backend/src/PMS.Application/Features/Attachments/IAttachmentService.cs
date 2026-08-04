namespace PMS.Application.Features.Attachments;

public interface IAttachmentService
{
    Task<AttachmentResponse> UploadToTaskAsync(
        Guid taskId, UploadAttachmentRequest request, CancellationToken ct = default);

    Task<AttachmentResponse> UploadToProjectAsync(
        Guid projectId, UploadAttachmentRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<AttachmentResponse>> GetByTaskAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<AttachmentResponse>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);

    Task<AttachmentDownload> DownloadAsync(Guid attachmentId, CancellationToken ct = default);

    /// <summary>Xóa — luật per-row: người tải lên HOẶC ProjectManager (khuôn ADR-026).</summary>
    Task DeleteAsync(Guid attachmentId, CancellationToken ct = default);
}
