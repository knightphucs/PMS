using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Auth;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.Auth;

/// <summary>
/// Chỉ kiểm hai method mới của ADR-049 (đường ghi hồ sơ cá nhân). Phần đăng ký/đăng
/// nhập/refresh gắn chặt với cookie + HTTP nên đã được phủ ở tầng integration
/// (<c>tests/PMS.IntegrationTests/Auth/</c>), không lặp lại ở đây.
/// </summary>
public class AuthServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IEmployeeRepository _employeeRepo = Substitute.For<IEmployeeRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepo = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();

    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _uow.Employees.Returns(_employeeRepo);
        _uow.RefreshTokens.Returns(_refreshTokenRepo);
        _uow.Permissions.GetCodesForRoleAsync(Arg.Any<SystemRole>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)[]);

        _currentUser.EmployeeId.Returns(_employeeId);

        _tokenService.CreateAccessToken(Arg.Any<Employee>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns(new AccessTokenResult("access-token", DateTime.UtcNow.AddMinutes(15)));
        _tokenService.CreateRefreshToken()
            .Returns(new RefreshTokenResult("refresh-token", DateTime.UtcNow.AddDays(7)));
        _tokenService.HashToken(Arg.Any<string>()).Returns("hashed");

        _sut = new AuthService(
            _uow, _passwordHasher, _tokenService, _currentUser, _emailSender,
            new EmployeeMapper(), NullLogger<AuthService>.Instance);
    }

    private Employee NewEmployee(string name = "Tên Cũ") => new()
    {
        Id = _employeeId, Name = name, Email = "user@pms.test", PasswordHash = "old-hash",
    };

    [Fact]
    public async Task UpdateProfileAsync_doi_ten_thanh_cong_va_phat_token_moi()
    {
        var employee = NewEmployee();
        _employeeRepo.GetByIdAsync(_employeeId, Arg.Any<CancellationToken>()).Returns(employee);

        var result = await _sut.UpdateProfileAsync(new UpdateProfileRequest("Tên Mới"));

        employee.Name.ShouldBe("Tên Mới");
        result.Employee.Name.ShouldBe("Tên Mới");
        result.AccessToken.ShouldBe("access-token");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // Đổi tên không phải sự kiện bảo mật — không phiên nào bị thu hồi.
        await _refreshTokenRepo.DidNotReceive().GetActiveByEmployeeAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProfileAsync_ten_rong_nem_DomainException_khong_luu()
    {
        var employee = NewEmployee();
        _employeeRepo.GetByIdAsync(_employeeId, Arg.Any<CancellationToken>()).Returns(employee);

        await Should.ThrowAsync<PMS.Domain.Common.DomainException>(
            () => _sut.UpdateProfileAsync(new UpdateProfileRequest("   ")));

        employee.Name.ShouldBe("Tên Cũ");
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePasswordAsync_sai_mat_khau_hien_tai_nem_BusinessRuleException()
    {
        var employee = NewEmployee();
        _employeeRepo.GetByIdAsync(_employeeId, Arg.Any<CancellationToken>()).Returns(employee);
        _passwordHasher.Verify("sai", employee.PasswordHash).Returns(false);

        await Should.ThrowAsync<BusinessRuleException>(() => _sut.ChangePasswordAsync(
            new ChangePasswordRequest("sai", "Moi@Mk2026", "Moi@Mk2026")));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePasswordAsync_thanh_cong_thu_hoi_toan_bo_refresh_token_khac_va_phat_lai_token()
    {
        var employee = NewEmployee();
        _employeeRepo.GetByIdAsync(_employeeId, Arg.Any<CancellationToken>()).Returns(employee);
        _passwordHasher.Verify("Test@1234", employee.PasswordHash).Returns(true);
        _passwordHasher.Hash("Moi@Mk2026").Returns("new-hash");

        var otherSession = new RefreshToken { Id = Guid.NewGuid(), EmployeeId = _employeeId };
        _refreshTokenRepo.GetActiveByEmployeeAsync(_employeeId, Arg.Any<CancellationToken>())
            .Returns([otherSession]);

        var result = await _sut.ChangePasswordAsync(
            new ChangePasswordRequest("Test@1234", "Moi@Mk2026", "Moi@Mk2026"));

        employee.PasswordHash.ShouldBe("new-hash");
        otherSession.IsRevoked.ShouldBeTrue();
        result.AccessToken.ShouldBe("access-token");   // phiên hiện tại VẪN sống tiếp
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
