using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Authorization;
using PMS.Application.Features.Admin;
using PMS.Domain.Enums;

namespace PMS.API.Controllers;

/// <summary>
/// Quản trị ánh xạ vai trò hệ thống → quyền (ADR-045). Đây là thứ làm cho "đổi quyền của một
/// vai trò" thành thao tác trên DỮ LIỆU thay vì sửa một <c>switch</c> rồi deploy lại.
/// <para>
/// 🔴 Danh mục quyền là ĐÓNG: endpoint này chỉ đổi được ai có quyền gì, <b>không</b> tạo được
/// quyền mới. Thêm mã quyền phải đi qua <c>SystemPermissions</c> + <c>HasData</c> + migration,
/// và <c>SystemPermissionsCatalogTests</c> chặn mọi mã mang phạm vi project (ADR-042).
/// </para>
/// <para>
/// ⚠️ Thay đổi ở đây có hiệu lực với một người sau khi họ lấy access token mới — tối đa 15
/// phút, vì service thu hồi toàn bộ refresh token của vai trò đó. UI phải nói rõ cửa sổ này
/// chứ không được ngụ ý là tức thì.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = SystemPermissions.RolesManage)]
public class AdminPermissionsController : ControllerBase
{
    private readonly IRolePermissionAdminService _rolePermissions;

    public AdminPermissionsController(IRolePermissionAdminService rolePermissions)
        => _rolePermissions = rolePermissions;

    /// <summary>Danh mục quyền (mã + mô tả) — dựng nhãn cho ma trận checkbox.</summary>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PermissionResponse>>> GetCatalog(CancellationToken ct)
        => Ok(await _rolePermissions.GetCatalogAsync(ct));

    /// <summary>Ma trận đầy đủ: mọi vai trò kèm tập quyền hiện tại.</summary>
    [HttpGet("roles/permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<RolePermissionsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RolePermissionsResponse>>> GetMatrix(CancellationToken ct)
        => Ok(await _rolePermissions.GetMatrixAsync(ct));

    /// <summary>
    /// Ghi đè TOÀN BỘ tập quyền của một vai trò. Gửi thiếu mã nào là gỡ quyền đó.
    /// </summary>
    /// <response code="204">Đã lưu; refresh token của mọi người mang vai trò này bị thu hồi.</response>
    /// <response code="400">Danh sách chứa mã ngoài danh mục, hoặc có mã trùng lặp.</response>
    /// <response code="409">Gỡ <c>roles:manage</c> khỏi SystemAdmin — sẽ khóa vĩnh viễn lối vào quản trị.</response>
    [HttpPut("roles/{role}/permissions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        SystemRole role, UpdateRolePermissionsRequest request, CancellationToken ct)
    {
        await _rolePermissions.UpdateAsync(role, request, ct);
        return NoContent();
    }
}
