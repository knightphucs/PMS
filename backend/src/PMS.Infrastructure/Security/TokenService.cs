using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Security;

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        if (Encoding.UTF8.GetByteCount(_options.Secret) < 32)
            throw new InvalidOperationException(
                "Jwt:Secret phải dài tối thiểu 32 byte cho HMAC-SHA256.");
    }

    public AccessTokenResult CreateAccessToken(
        Employee employee, IReadOnlyCollection<string> permissions)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, employee.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, employee.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, employee.Name),

            // ClaimTypes.Role Ở LẠI, nhưng vai trò của nó đã đổi: từ 2026-08-04 (ADR-045) nó
            // là ĐỊNH DANH/HIỂN THỊ, không còn là trục phân quyền. Không policy nào đọc nó
            // nữa — `require-system-admin` đã bị xóa. Người đọc còn lại: AuthController.Me()
            // và menu người dùng ở frontend.
            new(ClaimTypes.Role, employee.SystemRole.ToString())
        };

        // MỖI quyền một claim, không phải một chuỗi ngăn cách bằng dấu cách: RequireClaim
        // khớp tự nhiên trên claim lặp, còn chuỗi gộp thì bắt phải viết một
        // IAuthorizationRequirement + handler riêng mà chẳng đổi lại được gì.
        claims.AddRange(permissions.Select(p => new Claim(SystemPermissions.ClaimType, p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var token = new JwtSecurityToken(
            _options.Issuer, _options.Audience, claims,
            notBefore: now, expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public RefreshTokenResult CreateRefreshToken()
        => new(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
               DateTime.UtcNow.AddDays(_options.RefreshTokenDays));

    // Base64Url: token đi trong query string của link reset nên không được chứa '+', '/', '='
    // — chúng phải percent-encode và rất dễ bị hỏng khi người dùng copy-paste từ email.
    public string CreateSecureToken()
        => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}