using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Infrastructure.Persistence.Repositories;

public class PermissionRepository : IPermissionRepository
{
    private readonly PmsDbContext _context;

    public PermissionRepository(PmsDbContext context) => _context = context;

    public async Task<IReadOnlyList<string>> GetCodesForRoleAsync(
        SystemRole role, CancellationToken ct = default)
        => await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.SystemRole == role)
            .Select(rp => rp.PermissionCode)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Permission>> GetCatalogAsync(CancellationToken ct = default)
        => await _context.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Code)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RolePermission>> GetAllGrantsAsync(CancellationToken ct = default)
        => await _context.RolePermissions
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task ReplaceGrantsForRoleAsync(
        SystemRole role, IReadOnlyCollection<string> codes, CancellationToken ct = default)
    {
        var current = await _context.RolePermissions
            .Where(rp => rp.SystemRole == role)
            .ToListAsync(ct);

        // Diff thay vì xóa-sạch-rồi-thêm-lại: xóa và chèn lại cùng một khóa chính trong một
        // lượt SaveChanges khiến EF sắp xếp lại thứ tự lệnh và có thể va khóa chính trùng.
        // Diff cũng làm nhật ký thay đổi của DB phản ánh đúng thứ thật sự đổi.
        foreach (var stale in current.Where(rp => !codes.Contains(rp.PermissionCode)))
            _context.RolePermissions.Remove(stale);

        var existing = current.Select(rp => rp.PermissionCode).ToHashSet();

        foreach (var code in codes.Where(c => !existing.Contains(c)))
            _context.RolePermissions.Add(
                new RolePermission { SystemRole = role, PermissionCode = code });
    }
}
