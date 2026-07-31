using PMS.Domain.Enums;

namespace PMS.Application.Features.Auth;

public record RegisterRequest(string Name, string Email, string Password, string ConfirmPassword);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);

public record EmployeeDto(Guid Id, string Name, string Email, SystemRole SystemRole);

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