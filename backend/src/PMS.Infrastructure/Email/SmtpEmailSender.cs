using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMS.Application.Common.Interfaces;

namespace PMS.Infrastructure.Email;

/// <summary>
/// Gửi email thật qua SMTP. Được chọn khi <see cref="SmtpOptions.IsConfigured"/> đúng —
/// xem <c>DependencyInjection.AddInfrastructure</c>.
///
/// <para>
/// 🔴 <b>KHÔNG BAO GIỜ ném exception ra ngoài.</b> Đây không phải sự cẩu thả mà là ràng buộc
/// của ADR-041: <c>ForgotPassword</c> phải trả 204 cho mọi kết quả, kể cả email không tồn
/// tại. Một exception ở đây thành 500, và "500 với email có thật / 204 với email bịa" chính
/// là kênh dò tài khoản mà ADR-041 sinh ra để bịt. <see cref="NullEmailSender"/> đã chọn im
/// lặng vì đúng lý do này; bản SMTP không được phá vỡ tính chất đó chỉ vì nó có thể thất bại
/// theo nhiều cách hơn.
/// </para>
/// <para>
/// Hệ quả phải chấp nhận: gửi thất bại thì người dùng vẫn thấy "đã gửi". Bù lại bằng log
/// mức <c>Error</c> — đó là chỗ người vận hành phải nhìn, không phải màn hình người dùng.
/// </para>
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string to, string subject, string body, CancellationToken ct = default)
    {
        try
        {
            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                // Timeout NGẮN có chủ đích: người dùng đang đứng chờ một request HTTP đồng bộ.
                // Mặc định 100 giây của SmtpClient sẽ giữ request quên-mật-khẩu treo đủ lâu để
                // chính độ trễ đó trở thành tín hiệu phân biệt "email có thật hay không".
                Timeout = _options.TimeoutSeconds * 1000,
                Credentials = string.IsNullOrWhiteSpace(_options.User)
                    ? null
                    : new NetworkCredential(_options.User, _options.Password),
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_options.ResolvedFrom, _options.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false,
            };
            message.To.Add(to);

            await client.SendMailAsync(message, ct);

            _logger.LogInformation("Đã gửi email '{Subject}' tới {To}", subject, to);
        }
        catch (Exception ex)
        {
            // KHÔNG log body — nó chứa link kèm token đặt lại mật khẩu dạng thô, đúng thứ mà
            // SerilogEmailSender bị cấm dùng ở production vì để lộ.
            _logger.LogError(ex,
                "Gửi email '{Subject}' tới {To} THẤT BẠI. Người dùng vẫn nhận phản hồi thành công " +
                "(ADR-041) — đây là chỗ duy nhất sự cố này hiện ra.", subject, to);
        }
    }
}
