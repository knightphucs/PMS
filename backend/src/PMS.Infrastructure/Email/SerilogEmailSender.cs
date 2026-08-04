using Microsoft.Extensions.Logging;
using PMS.Application.Common.Interfaces;

namespace PMS.Infrastructure.Email;

/// <summary>
/// Ghi email ra log thay vì gửi thật — để luồng đặt lại mật khẩu chạy được đầu-cuối khi
/// chưa cắm SMTP, và để demo/bảo vệ đồ án không phụ thuộc một dịch vụ ngoài.
/// <para>
/// 🔴 <b>CHỈ đăng ký ở Development/Testing.</b> Thân email chứa link kèm token THÔ; ở
/// production nó sẽ nằm trong <c>logs/pms-*.log</c>, và bất kỳ ai đọc được log — hoặc bất
/// kỳ hệ thống gom log nào — sẽ đặt lại được mật khẩu của mọi tài khoản. Việc gác môi
/// trường nằm ở <c>PMS.Infrastructure/DependencyInjection.cs</c>.
/// </para>
/// </summary>
public class SerilogEmailSender : IEmailSender
{
    private readonly ILogger<SerilogEmailSender> _logger;

    public SerilogEmailSender(ILogger<SerilogEmailSender> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[EMAIL GIẢ LẬP] Tới: {To} | Tiêu đề: {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Không làm gì — mặc định cho mọi môi trường ngoài Development/Testing, cho tới khi có
/// implementation SMTP thật.
/// <para>
/// Chọn "im lặng nuốt" thay vì "ném lỗi" có chủ đích: <c>ForgotPassword</c> luôn phải trả
/// 204 để không lộ email nào tồn tại (ADR-041), nên một exception ở đây sẽ biến thành 500 và
/// vô tình trở thành đúng cái kênh rò rỉ mà thiết kế đang chặn.
/// </para>
/// </summary>
public class NullEmailSender : IEmailSender
{
    private readonly ILogger<NullEmailSender> _logger;

    public NullEmailSender(ILogger<NullEmailSender> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        // KHÔNG log body — đó chính là thứ đang cần giữ kín.
        _logger.LogWarning(
            "Chưa cấu hình dịch vụ email: đã BỎ QUA email '{Subject}' gửi tới {To}", subject, to);
        return Task.CompletedTask;
    }
}
