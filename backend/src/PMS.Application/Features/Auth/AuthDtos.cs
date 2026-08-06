using PMS.Domain.Enums;

namespace PMS.Application.Features.Auth;

public record RegisterRequest(string Name, string Email, string Password, string ConfirmPassword);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Token, string NewPassword, string ConfirmPassword);

/// <summary>
/// Đường ghi hồ sơ cá nhân (ADR-049). Cố ý chỉ có <c>Name</c> — email không đổi được từ đây
/// (đổi email là thay đổi định danh đăng nhập, cần luồng xác minh riêng chưa có trong scope
/// này), và không có trường mật khẩu (xem <see cref="ChangePasswordRequest"/>).
/// </summary>
public record UpdateProfileRequest(string Name);

/// <summary>
/// Đổi mật khẩu khi ĐANG đăng nhập — khác <see cref="ResetPasswordRequest"/> ở chỗ người
/// dùng chứng minh danh tính bằng mật khẩu hiện tại, không phải bằng token gửi qua email.
/// </summary>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);

public record EmployeeDto(Guid Id, string Name, string Email, SystemRole SystemRole)
{
    /// <summary>
    /// Quyền tầng 1 của người dùng (ADR-045) — cùng tập với claim <c>permission</c> trong JWT,
    /// cùng một nguồn là bảng <c>RolePermissions</c>.
    /// <para>
    /// Có mặt ở đây để frontend gác nút/menu mà <b>không phải giải mã JWT</b>: access token
    /// nằm trong bộ nhớ Zustand và client chưa từng có một dòng nào đọc nội dung nó (ADR-027).
    /// Thêm bộ phân tích token ở client là thêm chỗ thứ hai để lệch. Cưỡng chế thật vẫn 100%
    /// ở server — đây chỉ để UI khỏi hiện nút chắc chắn sẽ nhận 403.
    /// </para>
    /// <para>
    /// 🔴 Là property <c>init</c> có mặc định <c>[]</c>, KHÔNG phải tham số positional thứ 5:
    /// Mapperly map từ <c>Employee</c> — thứ không có member nào tương ứng — nên tham số bắt
    /// buộc sẽ làm mapper không biên dịch được. Mặc định rỗng cũng khiến "quên điền" cho ra
    /// <b>không quyền nào</b> (fail-closed) thay vì NRE.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];
}

/// <summary>
/// Hợp đồng NỘI BỘ giữa <see cref="IAuthService"/> và tầng API — có chứa refresh token.
/// Không phải kiểu trả ra HTTP: <see cref="AuthenticatedResponse"/> mới là kiểu đó.
/// Xem ADR-027 — refresh token rời khỏi body JSON để XSS không đọc được nó.
/// </summary>
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    EmployeeDto Employee);

/// <summary>
/// Kiểu trả về của các endpoint auth. Cố ý KHÔNG có refresh token: nó đi bằng cookie
/// httpOnly (ADR-027). Nếu thêm lại vào đây thì XSS chỉ cần gọi /auth/refresh với
/// credentials:'include' rồi đọc body là chiếm được phiên 7 ngày — tức là cookie
/// httpOnly mất sạch tác dụng.
/// </summary>
public record AuthenticatedResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    EmployeeDto Employee)
{
    public static AuthenticatedResponse From(AuthResponse auth) =>
        new(auth.AccessToken, auth.AccessTokenExpiresAt, auth.Employee);
}