namespace PMS.Application.Features.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default);

    /// <summary>
    /// Yêu cầu đặt lại mật khẩu. <b>Không bao giờ ném</b> vì email không tồn tại — hành vi
    /// phải giống hệt nhau ở cả hai trường hợp, nếu không endpoint này trở thành công cụ dò
    /// email nào đã đăng ký (ADR-041).
    /// </summary>
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}