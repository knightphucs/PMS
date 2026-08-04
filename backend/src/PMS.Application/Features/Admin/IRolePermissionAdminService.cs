using PMS.Domain.Enums;

namespace PMS.Application.Features.Admin;

/// <summary>Đọc và sửa ánh xạ vai trò hệ thống → quyền (ADR-045).</summary>
public interface IRolePermissionAdminService
{
    /// <summary>Danh mục quyền (mã + mô tả), sắp theo mã.</summary>
    Task<IReadOnlyList<PermissionResponse>> GetCatalogAsync(CancellationToken ct = default);

    /// <summary>
    /// Ma trận đầy đủ: mọi vai trò, kèm tập quyền của từng vai trò. Trả tất cả thay vì có
    /// endpoint theo từng vai trò — màn quản trị luôn tải cả ma trận, và số vai trò đếm trên
    /// đầu ngón tay.
    /// </summary>
    Task<IReadOnlyList<RolePermissionsResponse>> GetMatrixAsync(CancellationToken ct = default);

    /// <summary>
    /// Ghi đè tập quyền của một vai trò. Thu hồi refresh token của mọi người mang vai trò đó.
    /// </summary>
    Task UpdateAsync(
        SystemRole role, UpdateRolePermissionsRequest request, CancellationToken ct = default);
}
