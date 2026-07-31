using PMS.Application.Common.Models;

namespace PMS.Application.Features.Comments;

public interface ICommentService
{
    Task<CommentResponse> CreateAsync(
        Guid taskId, CreateCommentRequest request, CancellationToken ct = default);

    Task<PagedResult<CommentResponse>> GetByTaskAsync(
        Guid taskId, PagedRequest request, CancellationToken ct = default);

    /// <summary>Chỉ tác giả sửa được (ADR-026) — PM cũng không sửa lời người khác.</summary>
    Task<CommentResponse> UpdateAsync(
        Guid id, UpdateCommentRequest request, CancellationToken ct = default);

    /// <summary>Tác giả hoặc ProjectManager; xóa cứng (ADR-026).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
