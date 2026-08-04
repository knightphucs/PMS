using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IAttachmentRepository : IRepository<Attachment>
{
    /// <summary>Nạp kèm <c>Uploader</c> — response cần tên người tải lên.</summary>
    Task<Attachment?> GetWithUploaderAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Attachment>> ListByTaskAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<Attachment>> ListByProjectAsync(Guid projectId, CancellationToken ct = default);
}
