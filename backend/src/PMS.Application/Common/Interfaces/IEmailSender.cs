namespace PMS.Application.Common.Interfaces;

/// <summary>
/// Gửi email. Có hai implementation và việc CHỌN cái nào là một quyết định bảo mật —
/// xem <c>PMS.Infrastructure/Email/</c> và ADR-041.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
