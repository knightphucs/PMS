namespace PMS.Application.Features.BoardColumns;

public interface IBoardColumnService
{
    Task<IReadOnlyList<BoardColumnResponse>> ListAsync(Guid projectId, CancellationToken ct = default);

    Task<BoardColumnResponse> CreateAsync(
        Guid projectId, CreateBoardColumnRequest request, CancellationToken ct = default);

    Task<BoardColumnResponse> UpdateAsync(
        Guid columnId, UpdateBoardColumnRequest request, CancellationToken ct = default);

    Task DeleteAsync(
        Guid columnId, DeleteBoardColumnRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<BoardColumnResponse>> ReorderAsync(
        Guid projectId, ReorderBoardColumnsRequest request, CancellationToken ct = default);
}
