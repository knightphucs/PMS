namespace PMS.Application.Common.Interfaces;

/// <summary>
/// Dựng URL trỏ về frontend cho các link gửi qua email (ví dụ link chấp nhận lời mời
/// project). Tách riêng khỏi <see cref="IEmailSender"/> và đặt ở Application vì service tầng
/// Application cần dựng link TRƯỚC khi gọi gửi email — implementation (đọc
/// <c>AppOptions.FrontendBaseUrl</c>) nằm ở Infrastructure, giữ đúng chiều phụ thuộc Clean
/// Architecture (Application không được biết tới <c>IOptions&lt;T&gt;</c> hay config nào cả).
/// </summary>
public interface IAppLinkBuilder
{
    /// <summary>Link trang public chấp nhận lời mời project, mang token thô: <c>{FrontendBaseUrl}/invitations/{rawToken}</c>.</summary>
    string BuildInvitationLink(string rawToken);
}
