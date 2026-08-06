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

    // ---------- Đường ghi hồ sơ cá nhân (ADR-049) ----------

    /// <summary>
    /// Đổi tên hiển thị của CHÍNH người gọi (lấy id từ <c>ICurrentUserService</c>, không nhận
    /// tham số employeeId — cùng nguyên tắc "không xem hộ người khác" như ADR-053). Trả về
    /// <see cref="AuthResponse"/> mới để controller phát lại token: <c>Name</c> nằm trong JWT
    /// claim nên phiên hiện tại phải có access token mới ngay, nếu không màn hồ sơ sẽ báo lưu
    /// thành công mà vẫn hiện tên cũ tới 15 phút.
    /// </summary>
    Task<AuthResponse> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default);

    /// <summary>
    /// Đổi mật khẩu khi đang đăng nhập. Thu hồi mọi phiên KHÁC (cùng tiền lệ
    /// <see cref="ResetPasswordAsync"/>) nhưng vẫn phát lại token cho chính tab đang thực hiện
    /// thao tác — người dùng còn phiên sống tiếp, chỉ các thiết bị/tab khác bị đăng xuất.
    /// </summary>
    Task<AuthResponse> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
}