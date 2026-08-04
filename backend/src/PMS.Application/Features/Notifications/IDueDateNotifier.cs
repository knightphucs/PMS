namespace PMS.Application.Features.Notifications;

/// <summary>
/// Quét task sắp đến hạn / quá hạn và sinh thông báo <c>DueSoon</c> (ADR-040).
/// <para>
/// Tách khỏi <c>BackgroundService</c> có chủ đích: <c>BackgroundService</c> chỉ còn là cái
/// đồng hồ, còn toàn bộ nghiệp vụ nằm ở đây nên gọi thẳng được từ test mà không phải chờ
/// timer, và sau này gắn vào Hangfire hay một endpoint admin cũng không phải viết lại.
/// </para>
/// </summary>
public interface IDueDateNotifier
{
    /// <returns>Số thông báo đã tạo trong lượt quét này.</returns>
    Task<int> RunAsync(CancellationToken ct = default);
}
