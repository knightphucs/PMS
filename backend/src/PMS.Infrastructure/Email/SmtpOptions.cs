namespace PMS.Infrastructure.Email;

/// <summary>
/// Cấu hình SMTP, bind từ section <c>"Smtp"</c>.
///
/// <para>
/// 🔴 <b>Không có giá trị mặc định nào cho <see cref="Host"/>.</b> Đó là công tắc quyết
/// định: <c>Host</c> rỗng nghĩa là "chưa cắm SMTP" và hệ thống rơi về
/// <see cref="NullEmailSender"/>. Đặt sẵn một host mặc định (kiểu <c>localhost</c>) sẽ biến
/// "chưa cấu hình" thành "cấu hình sai" — và hai thứ đó hỏng theo hai cách khác nhau: cái
/// đầu im lặng đúng như thiết kế, cái sau ném timeout 30 giây trong mỗi request quên mật khẩu.
/// </para>
/// <para>
/// ⚠️ <see cref="Password"/> KHÔNG bao giờ được đặt trong <c>appsettings*.json</c> commit
/// vào repo. Nạp qua biến môi trường <c>Smtp__Password</c> (hoặc user-secrets khi dev).
/// </para>
/// </summary>
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>Máy chủ SMTP. Rỗng = tắt hẳn việc gửi email thật.</summary>
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    /// <summary>STARTTLS. Chỉ tắt khi trỏ vào một relay nội bộ đã nằm trong mạng tin cậy.</summary>
    public bool EnableSsl { get; set; } = true;

    public string User { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Địa chỉ người gửi. Bỏ trống thì dùng <see cref="User"/> — đúng với phần lớn relay
    /// (Gmail, Brevo…) vốn bắt buộc From phải khớp tài khoản đã xác thực.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "PMS";

    /// <summary>Giây. Ngắn có chủ đích — xem chú thích trong <see cref="SmtpEmailSender"/>.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    public string ResolvedFrom => string.IsNullOrWhiteSpace(FromAddress) ? User : FromAddress;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
