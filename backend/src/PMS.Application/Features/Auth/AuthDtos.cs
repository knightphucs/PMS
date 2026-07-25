using PMS.Domain.Enums;

namespace PMS.Application.Features.Auth;

public record RegisterRequest(string Name, string Email, string Password, string ConfirmPassword);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);

public record EmployeeDto(Guid Id, string Name, string Email, SystemRole SystemRole);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    EmployeeDto Employee);