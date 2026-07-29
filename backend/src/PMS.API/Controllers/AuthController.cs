using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PMS.Application.Features.Auth;
using PMS.Domain.Enums;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register"), AllowAnonymous, EnableRateLimiting("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req, CancellationToken ct)
        => Ok(await _auth.RegisterAsync(req, ct));

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req, CancellationToken ct)
        => Ok(await _auth.LoginAsync(req, ct));

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest req, CancellationToken ct)
        => Ok(await _auth.RefreshAsync(req, ct));

    [HttpPost("logout"), Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshTokenRequest req, CancellationToken ct)
    {
        await _auth.LogoutAsync(req, ct);
        return NoContent();
    }

    [HttpGet("me"), Authorize]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    public ActionResult<EmployeeDto> Me()
    {
        var id = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email)!;
        var name = User.FindFirstValue(ClaimTypes.Name)!;
        var role = Enum.Parse<SystemRole>(User.FindFirstValue(ClaimTypes.Role)!);

        return Ok(new EmployeeDto(id, name, email, role));
    }
}