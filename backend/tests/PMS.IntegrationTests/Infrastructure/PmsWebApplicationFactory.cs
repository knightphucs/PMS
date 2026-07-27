// tests/PMS.IntegrationTests/Infrastructure/PmsWebApplicationFactory.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMS.Infrastructure.Persistence;

namespace PMS.IntegrationTests.Infrastructure;

public class PmsWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestConnection =
        "Server=localhost,1433;Database=PmsTestDb;User Id=sa;Password=Pms@Local2026;" +
        "TrustServerCertificate=True;Encrypt=False";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnection,
                ["Jwt:Secret"]         = "NGVrjVqNmpdKAoDyYxP4CkKQxptJKlkK",
                ["Jwt:Issuer"]         = "PMS.Test",
                ["Jwt:Audience"]       = "PMS.Test",
                ["Jwt:AccessTokenMinutes"]  = "15",
                ["Jwt:RefreshTokenDays"]    = "7"
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PmsDbContext>>();
            services.RemoveAll<PmsDbContext>();

            services.AddDbContext<PmsDbContext>(options =>
                options.UseSqlServer(TestConnection, sql =>
                    sql.MigrationsAssembly(typeof(PmsDbContext).Assembly.FullName)));
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
