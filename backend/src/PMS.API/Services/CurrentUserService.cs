using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Enums;

namespace PMS.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;
    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public Guid? EmployeeId =>
        Guid.TryParse(User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public SystemRole? SystemRole =>
        Enum.TryParse<SystemRole>(User?.FindFirstValue(ClaimTypes.Role), out var r) ? r : null;

    public string? IpAddress =>
        _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}