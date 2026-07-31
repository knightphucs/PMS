using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PMS.API.Filters;
using PMS.API.Middleware;
using PMS.API.Services;
using PMS.Application;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Enums;
using PMS.Infrastructure;
using PMS.Infrastructure.Persistence;
using PMS.Infrastructure.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);
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

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("can-create-project", policy => policy.RequireAuthenticatedUser())
    .AddPolicy("require-system-admin", policy => policy.RequireClaim(ClaimTypes.Role, nameof(SystemRole.SystemAdmin)))
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
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
});

builder.Services.AddHealthChecks();

// ADR-022: enum đi qua JSON dưới dạng TÊN, không phải số thứ tự. Converter có tác dụng
// hai chiều và vẫn nhận được số ở chiều request, nên client cũ không vỡ; đổi lại response
// và Swagger đều đọc được bằng mắt ("Review" thay vì 2) và frontend không phải tự dựng
// bảng map số -> tên ở mọi chỗ hiển thị.
builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
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
