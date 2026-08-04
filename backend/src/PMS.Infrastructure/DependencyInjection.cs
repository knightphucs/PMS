using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Attachments;
using PMS.Infrastructure.Email;
using PMS.Infrastructure.Persistence;
using PMS.Infrastructure.Persistence.Repositories;
using PMS.Infrastructure.Security;
using PMS.Infrastructure.Storage;

namespace PMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration,
        bool useFakeEmailSender = false)
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
        services.AddScoped<ISprintRepository, SprintRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        // Không đi qua IUnitOfWork: đây thuần là truy vấn tổng hợp đọc-only, không tham gia
        // vòng đời entity nào nên không có gì để "unit of work" cùng.
        services.AddScoped<IProjectStatisticsRepository, ProjectStatisticsRepository>();
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

        // Lưu trữ file đính kèm (ADR-035). Đổi sang S3/Azure Blob = thay đúng dòng
        // AddScoped<IFileStorage, ...> này, không đụng tầng Application.
        services.AddOptions<FileStorageOptions>()
                .Bind(configuration.GetSection(FileStorageOptions.SectionName))
                .Validate(o => o.MaxFileBytes > 0, "FileStorage:MaxFileBytes phải lớn hơn 0.")
                .Validate(o => o.AllowedExtensions.Length > 0,
                          "FileStorage:AllowedExtensions không được rỗng — whitelist rỗng nghĩa là chặn hết.")
                .Validate(o => o.AllowedExtensions.All(e => e.StartsWith('.')),
                          "FileStorage:AllowedExtensions phải bắt đầu bằng dấu chấm, ví dụ '.png'.")
                .ValidateOnStart();

        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IAttachmentPolicy, AttachmentPolicy>();

        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();

        // 🔴 Việc CHỌN implementation ở đây là một quyết định BẢO MẬT, không phải tiện lợi
        // (ADR-041). SerilogEmailSender ghi nguyên thân email — trong đó có token đặt lại
        // mật khẩu dạng THÔ — ra logs/pms-*.log. Ở production, ai đọc được log (hoặc bất kỳ
        // hệ thống gom log nào) sẽ đặt lại được mật khẩu của mọi tài khoản.
        // Mặc định là NullEmailSender; chỉ Development/Testing mới bật bản giả lập.
        if (useFakeEmailSender) services.AddScoped<IEmailSender, SerilogEmailSender>();
        else                    services.AddScoped<IEmailSender, NullEmailSender>();

        return services;
    }
}
