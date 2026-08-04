namespace PMS.Application.Features.Watchers;

public record WatcherResponse(Guid EmployeeId, string EmployeeName, DateTime CreatedAt);

/// <summary>
/// Trả về sau khi watch/unwatch để UI cập nhật nút và số đếm trong một lượt, không phải
/// gọi thêm <c>GET /watchers</c>.
/// </summary>
public record WatchStateResponse(bool IsWatching, int WatcherCount);
