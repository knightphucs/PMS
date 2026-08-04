using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PMS.API.BackgroundJobs;
using PMS.API.Filters;
using PMS.API.Middleware;
using PMS.API.Services;
using PMS.Application;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Enums;
using PMS.Infrastructure;
using PMS.Infrastructure.Persistence;
using PMS.Infrastructure.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// useFakeEmailSender: CHỈ Development/Testing. Bản giả lập ghi token đặt lại mật khẩu dạng
// thô ra log — xem chú thích trong AddInfrastructure (ADR-041).
builder.Services.AddInfrastructure(
    builder.Configuration,
    useFakeEmailSender: builder.Environment.IsDevelopment()
                     || builder.Environment.IsEnvironment("Testing"));
builder.Services.AddApplication();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// JWT
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
const string CorsPolicy = "PmsFrontend";

// Đọc origin qua options system (Configure<IConfiguration>) thay vì đọc thẳng
// builder.Configuration ở đây: bản đọc sớm chỉ thấy các nguồn cấu hình đã đăng ký tại
// đúng thời điểm dòng này chạy, nên nguồn thêm sau (vd AddInMemoryCollection của
// PmsWebApplicationFactory) bị bỏ qua — policy nhận mảng rỗng và không phát header nào.
// Hoãn tới lúc DI resolve thì luôn đọc được cấu hình cuối cùng của môi trường đang chạy.
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>()
    .Configure<IConfiguration>((options, configuration) =>
        options.AddPolicy(CorsPolicy, policy => policy
            .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()
            // Bắt buộc cho cookie refresh của ADR-027: thiếu nó thì trình duyệt vứt bỏ
            // Set-Cookie ở phản hồi cross-origin và không đính cookie vào request sau.
            // Hợp lệ vì đi kèm WithOrigins — AllowAnyOrigin + AllowCredentials bị cấm.
            .AllowCredentials()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,           ValidIssuer = jwt.Issuer,
            ValidateAudience = true,         ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => o.Secret.Length >= 32,
        "Jwt:Secret phải có tối thiểu 32 ký tự (HMAC-SHA256). "
      + "Local dev: dotnet user-secrets set \"Jwt:Secret\" \"...\"")
    .Validate(o => o.AccessTokenMinutes is > 0 and <= 60,
        "Jwt:AccessTokenMinutes phải trong khoảng 1–60.")
    .ValidateOnStart();

// Phân quyền tầng 1 (ADR-045): MỘT policy cho mỗi mã quyền, TÊN POLICY == MÃ QUYỀN.
//
// Thay cho hai policy viết tay trước đây: `require-system-admin` (kiểm ClaimTypes.Role) và
// `can-create-project` (RequireAuthenticatedUser — một no-op). Cả hai tên đã bị XÓA; đừng
// thêm lại, và nếu grep còn thấy chúng ở đâu thì đó là một 500 đang chờ (tên policy lạ ném
// InvalidOperationException lúc chạy chứ không phải lỗi biên dịch).
//
// Vòng lặp chứ không IAuthorizationPolicyProvider: với một danh mục ĐÓNG cỡ này, provider là
// máy móc không đổi lại được gì, còn vòng lặp giữ toàn bộ ánh xạ nằm gọn trong một chỗ nhìn
// thấy được.
var authorization = builder.Services.AddAuthorizationBuilder();

foreach (var permission in SystemPermissions.All)
    authorization.AddPolicy(permission, policy =>
        policy.RequireClaim(SystemPermissions.ClaimType, permission));

authorization.SetFallbackPolicy(new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .Build());


builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 5,
                QueueLimit = 0
            }));
    options.AddPolicy("register", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 5,
                QueueLimit = 0
            }));
    options.AddPolicy("refresh", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 10,
                QueueLimit = 0
            }));
    // Đặt lại mật khẩu (ADR-041). Chặt hơn login (3/phút): forgot-password là bề mặt để
    // DÒ email nào đã đăng ký, và reset-password là bề mặt để đoán token. Cả hai đều không
    // có lý do chính đáng nào để gọi nhiều lần trong một phút.
    options.AddPolicy("forgot-password", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 3,
                QueueLimit = 0
            }));
});

builder.Services.AddHealthChecks();

// Job quét task sắp/đã quá hạn -> Notification DueSoon (ADR-040).
// Đăng ký BÊN TRONG nhánh này, cùng kiểu gác với UseRateLimiter phía dưới: dưới
// PmsWebApplicationFactory (UseEnvironment("Testing")) hosted service đơn giản là KHÔNG
// TỒN TẠI. Nếu nó chạy, mỗi bộ integration test sẽ có một luồng nền ghi Notification xen
// vào giữa, và những test đếm delta thông báo sẽ đỏ ngẫu nhiên — loại hỏng khó chẩn đoán
// nhất vì nó phụ thuộc thời điểm.
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddHostedService<DueDateNotificationWorker>();

// ADR-022: enum đi qua JSON dưới dạng TÊN, không phải số thứ tự. Converter có tác dụng
// hai chiều và vẫn nhận được số ở chiều request, nên client cũ không vỡ; đổi lại response
// và Swagger đều đọc được bằng mắt ("Review" thay vì 2) và frontend không phải tự dựng
// bảng map số -> tên ở mọi chỗ hiển thị.
builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Giới hạn multipart cho upload file đính kèm (ADR-035).
// 🔴 CỐ Ý đặt CAO HƠN FileStorage:MaxFileBytes (20 MB): file quá khổ phải chạm tới luật
// nghiệp vụ và nhận 413 kèm thông điệp tiếng Việt, thay vì bị Kestrel/model binder cắt
// ngang bằng một lỗi khó hiểu. Mặc định MaxRequestBodySize của Kestrel là 30 MB nên vẫn
// còn khoảng trống — nếu sau này nâng quá 30 MB thì phải chỉnh cả Kestrel.
// ⚠️ TUYỆT ĐỐI không dùng [DisableRequestSizeLimit]: đó là bỏ hẳn chốt chặn cuối cùng
// chống upload làm cạn đĩa.
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 25 * 1024 * 1024;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http,
        Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
        Description = "Dán access token vào đây (không cần gõ 'Bearer ')."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
    await context.Database.MigrateAsync();
    await DbSeeder.SeedAsync(context);
}
app.UseSerilogRequestLogging();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds
        });
    }
}).AllowAnonymous();
app.UseHttpsRedirection();
// Phải truyền tên policy: UseCors() không tham số đi tìm DEFAULT policy, mà ở đây chỉ có
// policy đặt tên (không gọi AddDefaultPolicy). Không tìm thấy thì CorsMiddleware chỉ log
// rồi đi tiếp — không header CORS nào được phát, build vẫn sạch, không test nào đỏ.
app.UseCors(CorsPolicy);
if (!app.Environment.IsEnvironment("Testing"))
    app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program {}
