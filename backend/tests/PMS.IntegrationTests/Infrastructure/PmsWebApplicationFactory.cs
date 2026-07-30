// tests/PMS.IntegrationTests/Infrastructure/PmsWebApplicationFactory.cs
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PMS.Infrastructure.Persistence;

namespace PMS.IntegrationTests.Infrastructure;

public class PmsWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Credential dùng-rồi-bỏ cho container SQL Server local/CI, không phải secret thật.
    private static readonly string TestConnection =
        Environment.GetEnvironmentVariable("PMS_TEST_DB")
        ?? "Server=localhost,1433;Database=PmsTestDb;User Id=sa;Password=Pms@Local2026;" +
           "TrustServerCertificate=True;Encrypt=False";
    private static readonly string TestJwtSecret =
        Environment.GetEnvironmentVariable("PMS_TEST_JWT_SECRET")
        ?? "NGVrjVqNmpdKAoDyYxP4CkKQxptJKlkK";
    private const string TestJwtIssuer = "PMS.Test";
    private const string TestJwtAudience = "PMS.Test";

    /// <summary>
    /// Origin dùng cho CorsPolicyTests. Không có key này thì "Cors:AllowedOrigins" trả về
    /// rỗng, WithOrigins() không có origin nào khớp, và test CORS sẽ pass/fail vì lý do sai.
    /// </summary>
    public const string TestFrontendOrigin = "http://localhost:3000";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnection,
                ["Jwt:Secret"]         = TestJwtSecret,
                ["Jwt:Issuer"]         = TestJwtIssuer,
                ["Jwt:Audience"]       = TestJwtAudience,
                ["Jwt:AccessTokenMinutes"]  = "15",
                ["Jwt:RefreshTokenDays"]    = "7",
                ["Cors:AllowedOrigins:0"]   = TestFrontendOrigin
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PmsDbContext>>();
            services.RemoveAll<PmsDbContext>();

            services.AddDbContext<PmsDbContext>(options =>
                options.UseSqlServer(TestConnection, sql =>
                    sql.MigrationsAssembly(typeof(PmsDbContext).Assembly.FullName)));

            services.RemoveAll<IConfigureOptions<JwtBearerOptions>>();
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = TestJwtIssuer,
                    ValidateAudience = true, ValidAudience = TestJwtAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync() => await base.DisposeAsync();
}
