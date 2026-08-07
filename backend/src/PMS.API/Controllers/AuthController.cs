using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Features.Auth;
using PMS.Domain.Enums;
using PMS.Infrastructure.Security;

namespace PMS.API.Controllers;

/// <summary>
/// ⚠️ Route viết thường TƯỜNG MINH, không dùng <c>[controller]</c>. Routing của ASP.NET
/// không phân biệt hoa thường nhưng <c>Path</c> của cookie thì CÓ. Với <c>[controller]</c>
/// route là <c>/api/v1/Auth</c> (chữ A hoa), nên client gọi <c>/api/v1/auth/refresh</c>
/// sẽ không được trình duyệt đính cookie <c>Path=/api/v1/auth</c> vào — hỏng im lặng,
/// không lỗi, không exception. Cùng lớp lỗi với đính chính CORS cuối §15. Xem ADR-027.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    /// <summary>Tên cookie chứa refresh token. Test và frontend tham chiếu hằng này.</summary>
    public const string RefreshCookieName = "pms_refresh_token";

    /// <summary>
    /// Cookie chỉ được gửi tới đúng 4 endpoint auth, không đi kèm mọi request nghiệp vụ.
    /// Nhờ vậy các endpoint khác xác thực thuần bằng header Authorization và miễn nhiễm CSRF.
    /// </summary>
    public const string RefreshCookiePath = "/api/v1/auth";

    private readonly IAuthService _auth;
    private readonly JwtOptions _jwt;

    public AuthController(IAuthService auth, IOptions<JwtOptions> jwt)
        => (_auth, _jwt) = (auth, jwt.Value);

    [HttpPost("register"), AllowAnonymous, EnableRateLimiting("register")]
    [ProducesResponseType(typeof(AuthenticatedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthenticatedResponse>> Register(RegisterRequest req, CancellationToken ct)
        => Ok(IssueSession(await _auth.RegisterAsync(req, ct)));

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    [ProducesResponseType(typeof(AuthenticatedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthenticatedResponse>> Login(LoginRequest req, CancellationToken ct)
        => Ok(IssueSession(await _auth.LoginAsync(req, ct)));

    /// <summary>
    /// Không nhận body: refresh token chỉ đến từ cookie httpOnly. Rotation của
    /// <c>AuthService.RefreshAsync</c> có reuse detection, nên client BẮT BUỘC phải
    /// single-flight — gọi song song hai lần với cùng một token sẽ bị coi là token bị
    /// đánh cắp và thu hồi TOÀN BỘ phiên (ADR-030 mô tả phía client).
    /// </summary>
    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("refresh")]
    [ProducesResponseType(typeof(AuthenticatedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticatedResponse>> Refresh(CancellationToken ct)
        => Ok(IssueSession(await _auth.RefreshAsync(new RefreshTokenRequest(ReadRefreshCookie()), ct)));

    [HttpPost("logout"), Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _auth.LogoutAsync(new RefreshTokenRequest(ReadRefreshCookie()), ct);
        ClearRefreshCookie();
        return NoContent();
    }

    /// <summary>
    /// 🔴 <b>LUÔN 204</b>, dù email có tồn tại hay không (ADR-041). Trả 404 cho email lạ là
    /// biến endpoint này thành công cụ dò xem ai đã đăng ký hệ thống. Rate limit dùng chung
    /// policy với <c>login</c> vì cùng là bề mặt tấn công theo email.
    /// </summary>
    [HttpPost("forgot-password"), AllowAnonymous, EnableRateLimiting("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(req, ct);
        return NoContent();
    }

    /// <summary>
    /// Token sai / hết hạn / đã dùng đều trả CÙNG một 400 với cùng một thông điệp — phân
    /// biệt được ba trường hợp là xác nhận cho kẻ tấn công rằng một token có thật.
    /// </summary>
    [HttpPost("reset-password"), AllowAnonymous, EnableRateLimiting("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(req, ct);

        // Mọi phiên đã bị thu hồi phía server; xóa luôn cookie ở đây để trình duyệt không
        // giữ lại một refresh token nay đã chết.
        ClearRefreshCookie();
        return NoContent();
    }

    /// <summary>
    /// Đường ghi hồ sơ cá nhân (ADR-049). Trả <see cref="AuthenticatedResponse"/> mới qua
    /// <see cref="IssueSession"/> vì <c>Name</c> nằm trong JWT claim: không phát lại token thì
    /// màn hồ sơ báo lưu thành công mà vẫn hiện tên cũ tới 15 phút, kể cả sau F5 (phiên khôi
    /// phục bằng <c>/refresh</c> cũng dựng claim từ token cũ). <c>/auth/me</c> vẫn giữ nguyên
    /// đọc từ claim — quyết định này KHÔNG đụng bất biến mà <c>PermissionClaimTests</c> khóa.
    /// </summary>
    [HttpPut("profile"), Authorize]
    [ProducesResponseType(typeof(AuthenticatedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthenticatedResponse>> UpdateProfile(UpdateProfileRequest req, CancellationToken ct)
        => Ok(IssueSession(await _auth.UpdateProfileAsync(req, ct)));

    /// <summary>
    /// Đổi mật khẩu khi đang đăng nhập (khác <c>reset-password</c>: xác minh bằng mật khẩu
    /// hiện tại, không phải token gửi qua email). Thu hồi mọi phiên KHÁC nhưng phát lại token
    /// cho chính tab này — người dùng còn phiên sống tiếp.
    /// </summary>
    [HttpPost("change-password"), Authorize]
    [ProducesResponseType(typeof(AuthenticatedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthenticatedResponse>> ChangePassword(ChangePasswordRequest req, CancellationToken ct)
        => Ok(IssueSession(await _auth.ChangePasswordAsync(req, ct)));

    [HttpGet("me"), Authorize]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    public ActionResult<EmployeeDto> Me()
    {
        var id = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email)!;
        var name = User.FindFirstValue(ClaimTypes.Name)!;
        var role = Enum.Parse<SystemRole>(User.FindFirstValue(ClaimTypes.Role)!);

        // 🔴 Endpoint này dựng DTO từ CLAIM chứ không đọc DB — nên `Permissions` cũng PHẢI
        // lấy từ claim. Chỉ nối dây ở AuthService thì /auth/login trả quyền thật còn /auth/me
        // trả mảng rỗng: hai câu trả lời mâu thuẫn từ cùng một kiểu DTO, và người dùng thấy
        // cái nào phụ thuộc vào việc họ vừa đăng nhập hay vừa F5. Không có gì bắt lỗi này
        // lúc biên dịch — PermissionClaimTests khóa nó bằng một test đối chiếu hai endpoint.
        //
        // Lấy từ claim cũng ĐÚNG về mặt ngữ nghĩa: /auth/me mô tả phiên hiện tại, mà quyền
        // của phiên hiện tại chính là quyền đã ký trong token, không phải quyền mới nhất
        // trong DB (thứ chỉ có hiệu lực từ token kế tiếp).
        var permissions = User.FindAll(SystemPermissions.ClaimType)
                              .Select(c => c.Value)
                              .ToArray();

        return Ok(new EmployeeDto(id, name, email, role) { Permissions = permissions });
    }

    /// <summary>Đặt cookie refresh mới rồi lược refresh token khỏi thân phản hồi.</summary>
    private AuthenticatedResponse IssueSession(AuthResponse auth)
    {
        Response.Cookies.Append(RefreshCookieName, auth.RefreshToken, BuildCookieOptions(
            DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenDays)));

        return AuthenticatedResponse.From(auth);
    }

    private void ClearRefreshCookie()
        => Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions(DateTimeOffset.UnixEpoch));

    /// <summary>
    /// Bốn thuộc tính phải khớp NGUYÊN VẸN giữa lúc đặt và lúc xóa, nếu không trình duyệt
    /// coi là hai cookie khác nhau và cookie cũ vẫn sống tiếp sau khi đăng xuất.
    /// </summary>
    private static CookieOptions BuildCookieOptions(DateTimeOffset expires) => new()
    {
        HttpOnly = true,                    // JavaScript không đọc được -> XSS không lấy được phiên 7 ngày
        Secure = true,                      // chỉ đi trên HTTPS, bắt buộc để dùng SameSite=None
        SameSite = SameSiteMode.Strict,      // chặn CSRF tới chính endpoint refresh
                                              // 🔴 Chỉ đúng khi frontend proxy /api/* qua chính domain của nó
                                              // (xem next.config.ts rewrite phía frontend) khiến trình duyệt thấy
                                              // request same-origin. Nếu frontend gọi THẲNG ra domain backend
                                              // (khác domain -> cross-site), Strict/Lax sẽ khiến trình duyệt
                                              // không bao giờ gửi cookie này -> /refresh 401 liên tục sau reload.
                                              // Trường hợp đó phải đổi sang SameSite=None (bắt buộc kèm Secure).
        Path = RefreshCookiePath,
        Expires = expires
    };

    private string ReadRefreshCookie()
        => Request.Cookies.TryGetValue(RefreshCookieName, out var token) && !string.IsNullOrWhiteSpace(token)
            ? token
            : throw new UnauthorizedException("Phiên đăng nhập không hợp lệ, vui lòng đăng nhập lại.");
}
