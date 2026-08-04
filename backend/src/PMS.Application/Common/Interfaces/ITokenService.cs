using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public record AccessTokenResult(string Token, DateTime ExpiresAt);
public record RefreshTokenResult(string Token, DateTime ExpiresAt);

public interface ITokenService
{
    /// <summary>
    /// Ký access token cho <paramref name="employee"/>, mang sẵn tập quyền tầng 1 (ADR-045).
    /// <para>
    /// <paramref name="permissions"/> được TRUYỀN VÀO chứ không tự tra: đây là bộ mã hóa
    /// thuần, nó ký thứ được bảo ký. Quyết định "người này có những quyền gì" là chuyện của
    /// tầng Application (<c>AuthService.BuildTokensAsync</c>) — đẩy nó vào đây là kéo
    /// <c>PmsDbContext</c> vào một component tuần tự hóa và làm nó không test được nếu không
    /// có database.
    /// </para>
    /// </summary>
    AccessTokenResult CreateAccessToken(Employee employee, IReadOnlyCollection<string> permissions);
    RefreshTokenResult CreateRefreshToken();

    /// <summary>
    /// Token ngẫu nhiên an toàn mật mã, an toàn khi đặt trong URL (base64url, không có
    /// <c>+ / =</c>). Dùng cho link đặt lại mật khẩu (ADR-041).
    /// </summary>
    string CreateSecureToken();

    /// <summary>
    /// SHA-256 dạng hex. Dùng cho <b>cả</b> refresh token lẫn token đặt lại mật khẩu — đổi
    /// tên từ <c>HashRefreshToken</c> khi có người dùng thứ hai, thay vì thêm một method
    /// thứ hai làm cùng một việc rồi để hai bản lệch nhau.
    /// </summary>
    string HashToken(string rawToken);
}