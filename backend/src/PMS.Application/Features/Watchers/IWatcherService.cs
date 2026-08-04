namespace PMS.Application.Features.Watchers;

public interface IWatcherService
{
    Task<IReadOnlyList<WatcherResponse>> GetByTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Tự theo dõi. Idempotent — đã theo dõi rồi thì không phải lỗi.</summary>
    Task<WatchStateResponse> WatchAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Tự bỏ theo dõi. Idempotent.</summary>
    Task<WatchStateResponse> UnwatchAsync(Guid taskId, CancellationToken ct = default);
}
