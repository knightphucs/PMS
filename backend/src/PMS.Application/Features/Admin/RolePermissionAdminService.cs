using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Admin;

public class RolePermissionAdminService : IRolePermissionAdminService
{
    /// <summary>
    /// 🔴 Bất biến chống tự khóa: <see cref="SystemRole.SystemAdmin"/> luôn phải giữ
    /// <see cref="SystemPermissions.RolesManage"/>.
    /// <para>
    /// Đây là quyền TỰ PHỤC HỒI duy nhất — còn nó thì cấp lại được mọi quyền khác qua UI.
    /// Mất nó thì màn phân quyền không vào được nữa, và không còn đường ghi nào khác trong
    /// hệ thống: <c>DbSeeder</c> không chạy ở production, còn <c>HasData</c> chỉ áp lúc chạy
    /// migration mới. Phục hồi sẽ phải sửa bảng <c>RolePermissions</c> bằng tay trong SSMS.
    /// </para>
    /// <para>
    /// Cố ý giữ bất biến TỐI THIỂU (chỉ đúng một mã, không kèm <c>employees:manage</c>): bất
    /// biến quá rộng là cách một mô hình permission lặng lẽ trở lại thành mô hình role cứng.
    /// </para>
    /// </summary>
    private const string SelfRecoveringPermission = SystemPermissions.RolesManage;

    private readonly IUnitOfWork _uow;
    private readonly IActivityLogger _activityLog;
    private readonly ILogger<RolePermissionAdminService> _logger;

    public RolePermissionAdminService(
        IUnitOfWork uow, IActivityLogger activityLog, ILogger<RolePermissionAdminService> logger)
    {
        _uow = uow;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PermissionResponse>> GetCatalogAsync(CancellationToken ct = default)
    {
        // KHÔNG kiểm quyền ở đây — policy `roles:manage` ở controller đã chặn. Đây là quyền
        // tầng 1, không phải quyền theo tài nguyên như Project (tiền lệ EmployeeAdminService).
        var catalog = await _uow.Permissions.GetCatalogAsync(ct);
        return catalog.Select(p => new PermissionResponse(p.Code, p.Description)).ToList();
    }

    public async Task<IReadOnlyList<RolePermissionsResponse>> GetMatrixAsync(CancellationToken ct = default)
    {
        var grants = await _uow.Permissions.GetAllGrantsAsync(ct);

        // Duyệt theo Enum.GetValues chứ không group theo dữ liệu: một vai trò chưa được cấp
        // quyền nào vẫn phải xuất hiện với danh sách rỗng, nếu không màn quản trị sẽ không
        // hiện dòng đó và người dùng không có cách nào cấp quyền cho nó.
        return Enum.GetValues<SystemRole>()
            .Select(role => new RolePermissionsResponse(
                role,
                grants.Where(g => g.SystemRole == role)
                      .Select(g => g.PermissionCode)
                      .OrderBy(c => c)
                      .ToList()))
            .ToList();
    }

    public async Task UpdateAsync(
        SystemRole role, UpdateRolePermissionsRequest request, CancellationToken ct = default)
    {
        var codes = request.Permissions
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();

        // Chốt chặn thứ hai sau validator. Từ chối CẢ LÔ chứ không lặng lẽ bỏ mã lạ: bỏ qua
        // im lặng chính là cách một danh mục "đóng" mục ruỗng thành mở.
        var unknown = codes.Where(c => !SystemPermissions.All.Contains(c)).ToList();
        if (unknown.Count > 0)
            throw new BusinessRuleException(
                $"Mã quyền không tồn tại trong danh mục: {string.Join(", ", unknown)}.");

        if (role == SystemRole.SystemAdmin && !codes.Contains(SelfRecoveringPermission))
            throw new ConflictException(
                $"Không thể gỡ `{SelfRecoveringPermission}` khỏi SystemAdmin — đây là quyền duy "
              + "nhất mở lại được màn hình phân quyền. Gỡ nó là khóa vĩnh viễn mọi lối vào quản trị.");

        var before = await _uow.Permissions.GetCodesForRoleAsync(role, ct);

        await _uow.Permissions.ReplaceGrantsForRoleAsync(role, codes, ct);

        // Quyền nằm trong JWT nên access token đã phát vẫn mang tập quyền CŨ. Thu hồi refresh
        // token của mọi người mang vai trò này kéo cửa sổ rủi ro xuống còn đúng tuổi thọ
        // access token (15 phút) — cùng cách xử lý và cùng lý do với
        // EmployeeAdminService.ChangeSystemRoleAsync (ADR-015).
        var revoked = 0;
        foreach (var token in await _uow.RefreshTokens.GetActiveByRoleAsync(role, ct))
        {
            token.Revoke();
            revoked++;
        }

        var added = codes.Except(before).OrderBy(c => c).ToList();
        var removed = before.Except(codes).OrderBy(c => c).ToList();

        // EntityId = Guid.Empty: một SystemRole không có Guid, mà IActivityLogger.Log yêu cầu
        // một cái. Loại entity "RolePermission" nằm trong SystemScopedEntityTypes nên dòng
        // này ĐỌC ĐƯỢC ở /admin/audit-logs — cố ý: đây là thao tác nhạy cảm nhất hệ thống,
        // để nó vô hình trong nhật ký thì mâu thuẫn với chính lý do endpoint đó tồn tại.
        _activityLog.Log(
            nameof(RolePermission), Guid.Empty, ActivityAction.PermissionsChanged,
            $"Đổi quyền của vai trò {role}. "
          + $"Thêm: {Describe(added)}. Gỡ: {Describe(removed)}.");

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Đổi quyền vai trò {Role}: thêm [{Added}], gỡ [{Removed}]; thu hồi {Count} refresh token",
            role, string.Join(",", added), string.Join(",", removed), revoked);
    }

    private static string Describe(IReadOnlyCollection<string> codes)
        => codes.Count == 0 ? "(không)" : string.Join(", ", codes);
}
