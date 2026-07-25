// Features/Auth/AuthService.cs
using Microsoft.Extensions.Logging;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Auth;

public class AuthService : IAuthService
{
    // Hash giả của một mật khẩu ngẫu nhiên. Dùng khi email không tồn tại, để thời gian
    // phản hồi của "sai email" ~ "sai mật khẩu" -> chặn timing attack dò email hợp lệ.
    private const string DummyHash =
        "$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";

    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUserService _currentUser;
    private readonly EmployeeMapper _mapper;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork uow, IPasswordHasher passwordHasher, ITokenService tokenService,
        ICurrentUserService currentUser, EmployeeMapper mapper, ILogger<AuthService> logger)
    {
        _uow = uow; _passwordHasher = passwordHasher; _tokenService = tokenService;
        _currentUser = currentUser; _mapper = mapper; _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();

        if (await _uow.Employees.EmailExistsAsync(email, ct))
            throw new ConflictException("Email này đã được đăng ký.");

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            SystemRole = SystemRole.User   // KHÔNG lấy từ request
        };

        await _uow.Employees.AddAsync(employee, ct);

        // Id sinh phía app (Guid.NewGuid) nên tạo được RefreshToken tham chiếu employee
        // ngay lập tức -> chỉ cần 1 lần SaveChanges, tự động nguyên tử, không cần transaction.
        var (response, _) = BuildTokens(employee);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Đăng ký tài khoản mới: {EmployeeId}", employee.Id);
        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var employee = await _uow.Employees.GetByEmailAsync(request.Email.Trim(), ct);

        if (employee is null)
        {
            _passwordHasher.Verify(request.Password, DummyHash);   // đốt thời gian tương đương
            throw new UnauthorizedException("Email hoặc mật khẩu không đúng.");
        }

        if (!_passwordHasher.Verify(request.Password, employee.PasswordHash))
        {
            _logger.LogWarning("Đăng nhập thất bại cho {Email} từ {Ip}",
                employee.Email, _currentUser.IpAddress);
            throw new UnauthorizedException("Email hoặc mật khẩu không đúng.");
        }

        var (response, _) = BuildTokens(employee);
        await _uow.SaveChangesAsync(ct);
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var hash = _tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await _uow.RefreshTokens.GetByHashAsync(hash, ct)
            ?? throw new UnauthorizedException("Refresh token không hợp lệ.");

        if (stored.IsRevoked)
        {
            // Token đã thu hồi mà vẫn được dùng => nhiều khả năng đã bị đánh cắp.
            // Xử lý: hủy toàn bộ session của user, buộc đăng nhập lại.
            await RevokeAllAsync(stored.EmployeeId, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Phát hiện tái sử dụng refresh token của {EmployeeId} từ {Ip} — đã thu hồi toàn bộ session",
                stored.EmployeeId, _currentUser.IpAddress);

            throw new UnauthorizedException("Phiên đăng nhập không hợp lệ, vui lòng đăng nhập lại.");
        }

        if (stored.IsExpired)
            throw new UnauthorizedException("Phiên đăng nhập đã hết hạn.");

        var employee = await _uow.Employees.GetByIdAsync(stored.EmployeeId, ct)
            ?? throw new UnauthorizedException("Tài khoản không còn tồn tại.");

        var (response, newToken) = BuildTokens(employee);
        stored.Revoke(newToken.Id);                    // rotation: token cũ chết ngay
        await _uow.SaveChangesAsync(ct);

        return response;
    }

    public async Task LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var hash = _tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await _uow.RefreshTokens.GetByHashAsync(hash, ct);
        if (stored is null) return;                    // idempotent, không lộ token nào tồn tại

        stored.Revoke();
        await _uow.SaveChangesAsync(ct);
    }

    // ---------- private ----------

    private (AuthResponse Response, RefreshToken Entity) BuildTokens(Employee employee)
    {
        var access = _tokenService.CreateAccessToken(employee);
        var refresh = _tokenService.CreateRefreshToken();

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            TokenHash = _tokenService.HashRefreshToken(refresh.Token),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = refresh.ExpiresAt,
            CreatedByIp = _currentUser.IpAddress
        };

        // AddAsync không gọi await SaveChanges — chỉ đưa vào ChangeTracker.
        _uow.RefreshTokens.Add(entity);

        return (new AuthResponse(access.Token, refresh.Token, access.ExpiresAt, _mapper.ToDto(employee)), entity);
    }

    private async Task RevokeAllAsync(Guid employeeId, CancellationToken ct)
    {
        foreach (var token in await _uow.RefreshTokens.GetActiveByEmployeeAsync(employeeId, ct))
            token.Revoke();
    }
}