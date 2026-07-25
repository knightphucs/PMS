using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Common.Interfaces;
using PMS.Infrastructure.Persistence;
using PMS.Infrastructure.Persistence.Repositories;
using PMS.Infrastructure.Security;

namespace PMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Thiếu ConnectionStrings:DefaultConnection. Chạy: dotnet user-secrets set ...");

        services.AddDbContext<PmsDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(PmsDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(maxRetryCount: 3);
            }));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddOptions<JwtOptions>()
                .Bind(configuration.GetSection(JwtOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.Secret)
                            && Encoding.UTF8.GetByteCount(o.Secret) >= 32,
                            "Jwt:Secret phải tồn tại và dài tối thiểu 32 byte cho HMAC-SHA256.")
                .Validate(o => o.AccessTokenMinutes is > 0 and <= 60,
                        "Jwt:AccessTokenMinutes phải trong khoảng 1-60.")
                .ValidateOnStart();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();

        return services;
    }
}
