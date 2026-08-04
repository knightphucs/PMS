using PMS.Domain.Enums;

namespace PMS.Application.Features.Admin;

public record LockAccountRequest(string Reason);
public record ChangeSystemRoleRequest(SystemRole Role);

public record EmployeeAdminResponse(
    Guid Id, string Name, string Email, SystemRole SystemRole,
    bool IsLocked, DateTime? LockedAt, string? LockReason, DateTime CreatedAt);

// ---------- Phân quyền vai trò (ADR-045) ----------

/// <summary>Một dòng của danh mục quyền — dựng nhãn cạnh mỗi ô tích ở màn quản trị.</summary>
public record PermissionResponse(string Code, string Description);

/// <summary>Tập quyền hiện tại của một vai trò.</summary>
public record RolePermissionsResponse(SystemRole Role, IReadOnlyList<string> Permissions);

/// <summary>
/// Thay TOÀN BỘ tập quyền của một vai trò — ghi đè, không phải delta.
/// <para>
/// Soi gương ADR-044 ("một trục ghi duy nhất, ghi đè toàn phần"): ma trận checkbox ánh xạ
/// tự nhiên vào replace, còn add/remove riêng lẻ cần hai endpoint và một câu chuyện tranh
/// chấp mà không đổi lại được gì.
/// </para>
/// </summary>
public record UpdateRolePermissionsRequest(IReadOnlyList<string> Permissions);