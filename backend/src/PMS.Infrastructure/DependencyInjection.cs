using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Attachments;
using PMS.Infrastructure.Configuration;
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

        // 🔴 ValidateOnStart, không phải kiểm lúc dùng. FrontendBaseUrl chỉ được đọc ở đúng
        // một chỗ — link trong email mời — nên một giá trị rỗng KHÔNG làm gì hỏng cho tới khi
        // có người bấm "mời qua email", và thứ hỏng khi đó là một lá thư đã gửi đi rồi, mang
        // link tương đối "/invitations/{token}" không mở được. Không hoàn tác được.
        // Thà không khởi động nổi còn hơn khởi động rồi gửi thư hỏng.
        services.AddOptions<AppOptions>()
                .Bind(configuration.GetSection(AppOptions.SectionName))
                .Validate(o => Uri.TryCreate(o.FrontendBaseUrl, UriKind.Absolute, out var uri)
                            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                          "App:FrontendBaseUrl phải là URL tuyệt đối http(s), ví dụ " +
                          "'https://pms-six-gamma.vercel.app'. Production nạp qua biến môi " +
                          "trường App__FrontendBaseUrl.")
                .ValidateOnStart();
        services.AddScoped<IAppLinkBuilder, AppLinkBuilder>();

        // 🔴 Việc CHỌN implementation ở đây là một quyết định BẢO MẬT, không phải tiện lợi
        // (ADR-041). SerilogEmailSender ghi nguyên thân email — trong đó có token đặt lại
        // mật khẩu dạng THÔ — ra logs/pms-*.log. Ở production, ai đọc được log (hoặc bất kỳ
        // hệ thống gom log nào) sẽ đặt lại được mật khẩu của mọi tài khoản.
        //
        // Ba nhánh, xét theo đúng thứ tự này (ADR-058):
        //   1. useFakeEmailSender (Development/Testing) -> Serilog, KHÔNG gửi ra ngoài thật.
        //      Đặt TRƯỚC nhánh SMTP có chủ đích: máy dev có thể vô tình thừa hưởng biến môi
        //      trường Smtp__* của production, và một lần chạy test bắn email thật tới người
        //      dùng thật là loại tai nạn không hoàn tác được.
        //   2. Smtp:Host có giá trị -> gửi thật.
        //   3. còn lại -> Null, im lặng nuốt.
        services.AddOptions<SmtpOptions>()
                .Bind(configuration.GetSection(SmtpOptions.SectionName))
                .Validate(o => !o.IsConfigured || !string.IsNullOrWhiteSpace(o.ResolvedFrom),
                          "Đã khai Smtp:Host thì phải có Smtp:FromAddress hoặc Smtp:User — " +
                          "không có địa chỉ người gửi thì mọi thư đều bị relay từ chối.")
                .Validate(o => !o.IsConfigured || o.Port is > 0 and <= 65535,
                          "Smtp:Port phải trong khoảng 1-65535.")
                .ValidateOnStart();

        var smtp = configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>();

        if (useFakeEmailSender)          services.AddScoped<IEmailSender, SerilogEmailSender>();
        else if (smtp?.IsConfigured is true) services.AddScoped<IEmailSender, SmtpEmailSender>();
        else                             services.AddScoped<IEmailSender, NullEmailSender>();

        return services;
    }
}
