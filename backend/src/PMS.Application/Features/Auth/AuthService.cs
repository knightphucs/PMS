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

    /// <summary>Hạn của token đặt lại mật khẩu — ngắn có chủ đích (§10 chốt 15–30 phút).</summary>
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);

    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailSender _emailSender;
    private readonly EmployeeMapper _mapper;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork uow, IPasswordHasher passwordHasher, ITokenService tokenService,
        ICurrentUserService currentUser, IEmailSender emailSender,
        EmployeeMapper mapper, ILogger<AuthService> logger)
    {
        _uow = uow; _passwordHasher = passwordHasher; _tokenService = tokenService;
        _currentUser = currentUser; _emailSender = emailSender;
        _mapper = mapper; _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();

        if (await _uow.Employees.EmailExistsAsync(email, ct))
            throw new ConflictException("Email này đã được đăng ký.");

        var employee = Employee.Register(
            request.Name,
            request.Email,
            _passwordHasher.Hash(request.Password)
        );

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

        if (employee.IsLocked)
        {
            _logger.LogWarning("Tài khoản bị khóa {EmployeeId} thử đăng nhập từ {Ip}",
                employee.Id, _currentUser.IpAddress);
            throw new ForbiddenException(
                "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên hệ thống.");
        }

        var (response, _) = BuildTokens(employee);
        await _uow.SaveChangesAsync(ct);
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var hash = _tokenService.HashToken(request.RefreshToken);
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

        if (employee.IsLocked)
        {
            // Dọn sạch mọi session còn sót — phòng trường hợp token được cấp trước lúc khóa.
            await RevokeAllAsync(employee.Id, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogWarning("Tài khoản bị khóa {EmployeeId} thử refresh token từ {Ip}",
                employee.Id, _currentUser.IpAddress);

            throw new UnauthorizedException("Phiên đăng nhập không hợp lệ, vui lòng đăng nhập lại.");
        }

        var (response, newToken) = BuildTokens(employee);
        stored.Revoke(newToken.Id);                    // rotation: token cũ chết ngay
        await _uow.SaveChangesAsync(ct);

        return response;
    }

    public async Task LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var hash = _tokenService.HashToken(request.RefreshToken);
        var stored = await _uow.RefreshTokens.GetByHashAsync(hash, ct);
        if (stored is null) return;                    // idempotent, không lộ token nào tồn tại

        stored.Revoke();
        await _uow.SaveChangesAsync(ct);
    }

    // ---------- Đặt lại mật khẩu (ADR-041) ----------

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var employee = await _uow.Employees.GetByEmailAsync(request.Email.Trim(), ct);

        if (employee is null)
        {
            // Vẫn đốt đúng lượng công việc như nhánh thành công (sinh token + hash) rồi vứt
            // đi — cùng kỹ thuật DummyHash mà LoginAsync đã dùng. Không có bước này thì
            // thời gian phản hồi tự nó tố cáo email nào tồn tại, và việc trả 204 ở cả hai
            // nhánh chỉ là bảo mật trên giấy.
            _ = _tokenService.HashToken(_tokenService.CreateSecureToken());

            _logger.LogInformation(
                "Yêu cầu đặt lại mật khẩu cho email không tồn tại từ {Ip}", _currentUser.IpAddress);
            return;
        }

        // Token cũ còn treo bị vô hiệu: mỗi lần yêu cầu chỉ có ĐÚNG MỘT link còn hiệu lực,
        // nên bấm nhầm link cũ trong hộp thư không đổi được mật khẩu.
        foreach (var outstanding in await _uow.PasswordResetTokens.GetUsableByEmployeeAsync(employee.Id, ct))
            outstanding.Invalidate();

        var rawToken = _tokenService.CreateSecureToken();

        await _uow.PasswordResetTokens.AddAsync(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            TokenHash = _tokenService.HashToken(rawToken),   // chỉ lưu hash, không lưu token thô
            ExpiresAt = DateTime.UtcNow.Add(ResetTokenLifetime),
            CreatedByIp = _currentUser.IpAddress
        }, ct);

        await _uow.SaveChangesAsync(ct);

        await _emailSender.SendAsync(
            employee.Email,
            "Đặt lại mật khẩu PMS",
            $"Chào {employee.Name},\n\n" +
            $"Mã đặt lại mật khẩu của bạn: {rawToken}\n" +
            $"Mã có hiệu lực trong {ResetTokenLifetime.TotalMinutes:0} phút và chỉ dùng được một lần.\n\n" +
            "Nếu bạn không yêu cầu, hãy bỏ qua email này — mật khẩu hiện tại vẫn giữ nguyên.",
            ct);

        _logger.LogInformation("Đã cấp token đặt lại mật khẩu cho {EmployeeId}", employee.Id);
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken ct = default)
    {
        var stored = await _uow.PasswordResetTokens.GetByHashAsync(
            _tokenService.HashToken(request.Token), ct);

        // 🔴 MỘT thông điệp cho cả ba nguyên nhân (không tồn tại / hết hạn / đã dùng), và là
        // 400 chứ không 404. Phân biệt được ba trường hợp — hoặc trả 404 cho riêng trường hợp
        // đầu — là xác nhận cho kẻ tấn công rằng một token nào đó có thật.
        if (stored is null || !stored.IsUsable)
            throw new BusinessRuleException(
                "Mã đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Hãy yêu cầu mã mới.");

        var employee = stored.Employee;

        stored.MarkUsed();

        // Vô hiệu mọi token còn treo khác: đổi mật khẩu xong thì không link cũ nào được
        // dùng lại để đổi tiếp.
        foreach (var other in await _uow.PasswordResetTokens.GetUsableByEmployeeAsync(employee.Id, ct))
            other.Invalidate();

        employee.PasswordHash = _passwordHasher.Hash(request.NewPassword);

        // Thu hồi TOÀN BỘ phiên — tiền lệ ADR-015 (khóa tài khoản cũng thu hồi). Lý do đổi
        // mật khẩu thường là "nghi bị lộ", nên để phiên cũ sống tiếp là bỏ qua đúng mối đe
        // dọa mà người dùng đang cố xử lý. Dùng lại RevokeAllAsync thay vì chép lại vòng lặp.
        await RevokeAllAsync(employee.Id, ct);

        // Tài khoản bị khóa VẪN đặt lại được mật khẩu: từ chối ở đây là để lộ trạng thái
        // khóa cho người chỉ cầm địa chỉ email. Việc chặn nằm ở LoginAsync (403).
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Đặt lại mật khẩu thành công cho {EmployeeId} từ {Ip}, đã thu hồi mọi phiên",
            employee.Id, _currentUser.IpAddress);
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
            TokenHash = _tokenService.HashToken(refresh.Token),
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