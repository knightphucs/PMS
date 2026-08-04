using PMS.Application.Features.Notifications;

namespace PMS.API.BackgroundJobs;

/// <summary>
/// Cái đồng hồ cho <see cref="IDueDateNotifier"/> — toàn bộ nghiệp vụ nằm ở notifier, ở đây
/// chỉ có chu kỳ và xử lý lỗi (ADR-040).
/// <para>
/// Đặt ở <c>PMS.API</c> chứ không phải <c>PMS.Infrastructure</c> vì
/// <c>Microsoft.Extensions.Hosting.Abstractions</c> đi kèm sẵn Web SDK; để ở Infrastructure
/// thì phải thêm một package chỉ để có <c>BackgroundService</c>.
/// </para>
/// </summary>
public class DueDateNotificationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DueDateNotificationWorker> _logger;

    public DueDateNotificationWorker(
        IServiceScopeFactory scopeFactory, ILogger<DueDateNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job quét hạn task đã khởi động, chu kỳ {Interval}", Interval);

        // Chạy ngay một lượt rồi mới vào nhịp: khởi động lại app không phải chờ đủ một chu
        // kỳ mới có thông báo. An toàn vì việc khử trùng lặp dựa trên DB, không dựa trên
        // việc "đã chạy lần nào chưa".
        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunOnceAsync(stoppingToken);
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            // BackgroundService là SINGLETON; IDueDateNotifier và IUnitOfWork là scoped.
            // Phải tạo scope mỗi lượt — inject thẳng vào constructor sẽ giữ một DbContext
            // sống suốt vòng đời ứng dụng, và ChangeTracker của nó phình vô hạn.
            using var scope = _scopeFactory.CreateScope();
            var notifier = scope.ServiceProvider.GetRequiredService<IDueDateNotifier>();

            await notifier.RunAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Tắt ứng dụng — không phải lỗi.
        }
        catch (Exception ex)
        {
            // Nuốt lỗi ở ĐÂY là có chủ đích: exception thoát khỏi ExecuteAsync sẽ giết hẳn
            // hosted service, và nó chết IM LẶNG — một lần mất kết nối DB sẽ làm toàn bộ
            // thông báo hạn ngừng hoạt động cho tới lần deploy sau mà không ai biết.
            _logger.LogError(ex, "Lượt quét hạn task thất bại, sẽ thử lại ở chu kỳ sau");
        }
    }
}
