using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Admin;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;

namespace PMS.UnitTests.Application.Admin;

public class EmployeeAdminServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IEmployeeRepository _employeeRepo = Substitute.For<IEmployeeRepository>();
    private readonly IRefreshTokenRepository _tokenRepo = Substitute.For<IRefreshTokenRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IActivityLogger _activityLog = Substitute.For<IActivityLogger>();

    private readonly Guid _adminId = Guid.NewGuid();
    private readonly EmployeeAdminService _sut;

    public EmployeeAdminServiceTests()
    {
        _uow.Employees.Returns(_employeeRepo);
        _uow.RefreshTokens.Returns(_tokenRepo);
        _currentUser.EmployeeId.Returns(_adminId);

        // NSubstitute trả Task<List> null nếu không cấu hình -> foreach sẽ NRE.
        _tokenRepo.GetActiveByEmployeeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                  .Returns(new List<RefreshToken>());

        _sut = new EmployeeAdminService(
            _uow, _currentUser, _activityLog,
            new EmployeeAdminMapper(), NullLogger<EmployeeAdminService>.Instance);
    }

    private static Employee EmployeeWith(SystemRole role)
    {
        var e = Employee.Register("Nguyen Van B", $"{Guid.NewGuid():N}@pms.test", "hash");
        if (role != SystemRole.User) e.ChangeSystemRole(role);
        return e;
    }

    private static RefreshToken NewToken(Guid employeeId) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = employeeId,
        TokenHash = Guid.NewGuid().ToString("N"),
        ExpiresAt = DateTime.UtcNow.AddDays(7)
    };

    [Fact]
    public async Task LockAsync_thu_hoi_toan_bo_refresh_token()
    {
        var target = EmployeeWith(SystemRole.User);
        var tokens = new List<RefreshToken> { NewToken(target.Id), NewToken(target.Id) };
        _employeeRepo.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);
        _tokenRepo.GetActiveByEmployeeAsync(target.Id, Arg.Any<CancellationToken>()).Returns(tokens);

        await _sut.LockAsync(target.Id, new LockAccountRequest("Nghỉ việc"));

        target.IsLocked.ShouldBeTrue();
        // Khóa mà không thu hồi token = người bị khóa vẫn refresh được suốt 7 ngày
        tokens.ShouldAllBe(t => t.IsRevoked);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LockAsync_SystemAdmin_cuoi_cung_thi_409()
    {
        var admin = EmployeeWith(SystemRole.SystemAdmin);
        _employeeRepo.GetByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);
        _employeeRepo.CountActiveAdminsExceptAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(0);

        await Should.ThrowAsync<ConflictException>(
            () => _sut.LockAsync(admin.Id, new LockAccountRequest("Test")));

        admin.IsLocked.ShouldBeFalse();
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Khong_the_tu_khoa_hoac_tu_doi_role_cua_chinh_minh()
    {
        await Should.ThrowAsync<BusinessRuleException>(
            () => _sut.LockAsync(_adminId, new LockAccountRequest("Test")));
        await Should.ThrowAsync<BusinessRuleException>(
            () => _sut.ChangeSystemRoleAsync(_adminId, new ChangeSystemRoleRequest(SystemRole.User)));
    }

    [Fact]
    public async Task ChangeSystemRoleAsync_thu_hoi_token_vi_SystemRole_nam_trong_JWT()
    {
        var target = EmployeeWith(SystemRole.SystemAdmin);
        var tokens = new List<RefreshToken> { NewToken(target.Id) };
        _employeeRepo.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);
        _employeeRepo.CountActiveAdminsExceptAsync(target.Id, Arg.Any<CancellationToken>()).Returns(1);
        _tokenRepo.GetActiveByEmployeeAsync(target.Id, Arg.Any<CancellationToken>()).Returns(tokens);

        await _sut.ChangeSystemRoleAsync(target.Id, new ChangeSystemRoleRequest(SystemRole.User));

        tokens.ShouldAllBe(t => t.IsRevoked);
    }
}