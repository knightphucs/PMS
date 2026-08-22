using PMS.Application.Common.Models;

namespace PMS.Application.Features.SavedViews;

public interface ISavedViewService
{
    /// <summary>View chia sẻ của project + view riêng của chính người gọi.</summary>
    Task<IReadOnlyList<SavedViewResponse>> ListAsync(Guid projectId, CancellationToken ct = default);

    Task<SavedViewResponse> CreateAsync(
        Guid projectId, CreateSavedViewRequest request, CancellationToken ct = default);

    Task<SavedViewResponse> UpdateAsync(
        Guid viewId, UpdateSavedViewRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid viewId, CancellationToken ct = default);

    Task ReorderAsync(
        Guid projectId, ReorderSavedViewsRequest request, CancellationToken ct = default);

    /// <summary>Chạy một bộ lọc trên danh sách task của project.</summary>
    Task<PagedResult<TaskListItemResponse>> QueryTasksAsync(
        Guid projectId, TaskQueryRequest request, CancellationToken ct = default);
}
