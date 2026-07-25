namespace PMS.Infrastructure.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "PMS.API";
    public string Audience { get; set; } = "PMS.Client";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}