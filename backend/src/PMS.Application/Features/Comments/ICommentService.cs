using PMS.Application.Common.Models;

namespace PMS.Application.Features.Comments;

public interface ICommentService
{
    Task<CommentResponse> CreateAsync(
        Guid taskId, CreateCommentRequest request, CancellationToken ct = default);

    Task<PagedResult<CommentResponse>> GetByTaskAsync(
        Guid taskId, PagedRequest request, CancellationToken ct = default);

    Task<CommentResponse> UpdateAsync(
        Guid id, UpdateCommentRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
