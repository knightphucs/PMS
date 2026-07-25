using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public record AccessTokenResult(string Token, DateTime ExpiresAt);
public record RefreshTokenResult(string Token, DateTime ExpiresAt);

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(Employee employee);
    RefreshTokenResult CreateRefreshToken();
    string HashRefreshToken(string rawToken);
}