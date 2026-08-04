using Microsoft.Extensions.Logging;
using NSubstitute;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Admin;
using PMS.Domain.Entities;
using PMS.Domain.Enums;
using Shouldly;
using Xunit;

namespace PMS.UnitTests.Application.Admin;

public class RolePermissionAdminServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IPermissionRepository _permissions = Substitute.For<IPermissionRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IActivityLogger _activityLog = Substitute.For<IActivityLogger>();
    private readonly RolePermissionAdminService _sut;

    public RolePermissionAdminServiceTests()
    {
        _uow.Permissions.Returns(_permissions);
        _uow.RefreshTokens.Returns(_refreshTokens);

        _permissions.GetCodesForRoleAsync(Arg.Any<SystemRole>(), Arg.Any<CancellationToken>())
                    .Returns(Array.Empty<string>());
        _refreshTokens.GetActiveByRoleAsync(Arg.Any<SystemRole>(), Arg.Any<CancellationToken>())
                      .Returns(Array.Empty<RefreshToken>());

        _sut = new RolePermissionAdminService(
            _uow, _activityLog, Substitute.For<ILogger<RolePermissionAdminService>>());
    }

    [Fact]
    public async Task Go_roles_manage_khoi_SystemAdmin_nem_Conflict()
    {
        var request = new UpdateRolePermissionsRequest(
            SystemPermissions.All.Where(c => c != SystemPermissions.RolesManage).ToList());

        await Should.ThrowAsync<ConflictException>(
            () => _sut.UpdateAsync(SystemRole.SystemAdmin, request));

        // Không ghi gì cả — không replace, không thu hồi token.
        await _permissions.DidNotReceive().ReplaceGrantsForRoleAsync(
            Arg.Any<SystemRole>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Vai_tro_User_duoc_phep_go_het_quyen()
    {
        // Bất biến chống tự khóa chỉ áp cho SystemAdmin. Gỡ hết quyền của User là lựa chọn
        // hợp lệ của quản trị viên (hệ quả: không ai tạo được project nữa) — cấm nó là biến
        // một bất biến hẹp thành một luật rộng không ai yêu cầu.
        await _sut.UpdateAsync(SystemRole.User, new UpdateRolePermissionsRequest([]));

        await _permissions.Received(1).ReplaceGrantsForRoleAsync(
            SystemRole.User,
            Arg.Is<IReadOnlyCollection<string>>(c => c != null && c.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ma_ngoai_danh_muc_nem_BusinessRule_va_khong_ghi_gi()
    {
        var request = new UpdateRolePermissionsRequest(
            [SystemPermissions.ProjectsCreate, "projects:read:all"]);

        await Should.ThrowAsync<BusinessRuleException>(
            () => _sut.UpdateAsync(SystemRole.User, request));

        await _permissions.DidNotReceive().ReplaceGrantsForRoleAsync(
            Arg.Any<SystemRole>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Duong_thanh_cong_ghi_quyen_thu_hoi_token_va_ghi_nhat_ky()
    {
        var stillActive = new RefreshToken
        {
            Id = Guid.NewGuid(),
            EmployeeId = Guid.NewGuid(),
            TokenHash = "hash",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _refreshTokens.GetActiveByRoleAsync(SystemRole.SystemAdmin, Arg.Any<CancellationToken>())
                      .Returns(new[] { stillActive });

        await _sut.UpdateAsync(
            SystemRole.SystemAdmin, new UpdateRolePermissionsRequest(SystemPermissions.All.ToList()));

        await _permissions.Received(1).ReplaceGrantsForRoleAsync(
            SystemRole.SystemAdmin,
            Arg.Is<IReadOnlyCollection<string>>(c => c != null && c.Count == SystemPermissions.All.Count),
            Arg.Any<CancellationToken>());

        // Quyền nằm trong JWT nên không thu hồi refresh token là để cửa sổ dùng quyền cũ dài
        // bằng tuổi refresh token (7 ngày) thay vì tuổi access token (15 phút) — ADR-015.
        stillActive.RevokedAt.ShouldNotBeNull();

        _activityLog.Received(1).Log(
            nameof(RolePermission), Guid.Empty, ActivityAction.PermissionsChanged, Arg.Any<string>());

        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ma_trung_lap_va_khoang_trang_duoc_chuan_hoa()
    {
        await _sut.UpdateAsync(SystemRole.User, new UpdateRolePermissionsRequest(
            [SystemPermissions.ProjectsCreate, $"  {SystemPermissions.ProjectsCreate}  ", "", "   "]));

        await _permissions.Received(1).ReplaceGrantsForRoleAsync(
            SystemRole.User,
            Arg.Is<IReadOnlyCollection<string>>(c =>
                c != null && c.Count == 1 && c.Contains(SystemPermissions.ProjectsCreate)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ma_tran_liet_ke_MOI_vai_tro_ke_ca_vai_tro_chua_co_quyen_nao()
    {
        _permissions.GetAllGrantsAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new RolePermission
            {
                SystemRole = SystemRole.SystemAdmin, PermissionCode = SystemPermissions.RolesManage
            }
        });

        var matrix = await _sut.GetMatrixAsync();

        matrix.Select(r => r.Role).ShouldBe(Enum.GetValues<SystemRole>(), ignoreOrder: true);
        matrix.Single(r => r.Role == SystemRole.User).Permissions.ShouldBeEmpty();
    }
}
