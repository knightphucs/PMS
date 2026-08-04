namespace PMS.Application.Features.TaskLinks;

public interface ITaskLinkService
{
    Task<IReadOnlyList<TaskLinkResponse>> GetByTaskAsync(Guid taskId, CancellationToken ct = default);
    Task<TaskLinkResponse> CreateAsync(Guid taskId, CreateTaskLinkRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid linkId, CancellationToken ct = default);
}
