using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

public class SavedViewRepository : Repository<SavedView>, ISavedViewRepository
{
    public SavedViewRepository(PmsDbContext context) : base(context) { }

    public async Task<IReadOnlyList<SavedView>> ListVisibleAsync(
        Guid projectId, Guid viewerId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            .Include(v => v.Filters)
            .Include(v => v.Columns.OrderBy(c => c.Order))
            .Include(v => v.Owner)
            // Hai collection -> split query, nếu không số dòng nhân lên theo filters × columns.
            .AsSplitQuery()
            .Where(v => v.ProjectId == projectId && (v.IsShared || v.OwnerId == viewerId))
            .OrderBy(v => v.Order).ThenBy(v => v.Id)
            .ToListAsync(ct);

    public async Task<SavedView?> GetWithPartsAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(v => v.Filters)
            .Include(v => v.Columns.OrderBy(c => c.Order))
            .Include(v => v.Owner)
            .AsSplitQuery()
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<int> DeletePrivateViewsOfMemberAsync(
        Guid projectId, Guid employeeId, CancellationToken ct = default)
        // ExecuteDeleteAsync: một lệnh DELETE thẳng xuống DB. Hàng ở SavedViewFilters và
        // SavedViewColumns biến mất theo nhờ FK Cascade — xem SavedViewConfigurations.
        //
        // ⚠️ Bỏ qua query filter và SaveChanges của DbContext (ADR-024), nhưng ở đây đó là
        // điều ĐÚNG: SavedView không phải ISoftDeletable nên không có gì để "xoá mềm", và
        // đây là một thao tác dọn dẹp chứ không phải một hành động nghiệp vụ cần audit riêng
        // (người gọi đã ghi ActivityLog cho việc gỡ thành viên).
        => await DbSet
            .Where(v => v.ProjectId == projectId && v.OwnerId == employeeId && !v.IsShared)
            .ExecuteDeleteAsync(ct);
}
