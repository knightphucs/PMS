using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

public class ActivityLogRepository : Repository<ActivityLog>, IActivityLogRepository
{
    public ActivityLogRepository(PmsDbContext context) : base(context) { }

    public Task<PagedResult<ActivityLog>> GetPagedByEntityAsync(
        string entityType, Guid entityId, PagedRequest request, CancellationToken ct = default)
        => PageAsync(
            DbSet.Where(a => a.EntityType == entityType && a.EntityId == entityId),
            request, ct);

    public Task<PagedResult<ActivityLog>> GetPagedBySystemScopeAsync(
        IReadOnlyCollection<string> entityTypes, PagedRequest request, CancellationToken ct = default)
        => PageAsync(DbSet.Where(a => entityTypes.Contains(a.EntityType)), request, ct);

    /// <summary>
    /// Phân trang dùng chung. Sắp xếp <c>CreatedAt DESC</c> rồi <c>Id</c> — tie-break là bắt
    /// buộc vì nhiều dòng log của cùng một thao tác được ghi trong CÙNG một
    /// <c>SaveChanges</c> nên có <c>CreatedAt</c> giống hệt nhau; thiếu nó thì thứ tự giữa
    /// hai lần gọi cùng một trang có thể khác nhau.
    /// <para>
    /// 🔴 <c>Search</c> lọc trên <c>Detail</c> — thêm 2026-08-04. Trước đó repository này
    /// <b>nhận rồi bỏ qua im lặng</b> tham số đó ở cả ba endpoint dùng nó
    /// (<c>/tasks/{id}/activity</c>, <c>/projects/{id}/activity</c>,
    /// <c>/admin/audit-logs</c>): client gửi <c>?search=</c> và nhận về HTTP 200 kèm nguyên
    /// trang CHƯA lọc — sai một cách không thể phát hiện từ phía client. Đây là repository
    /// cuối cùng còn sót lại; 5 repository kia đều đã lọc thật.
    /// </para>
    /// </summary>
    private static async Task<PagedResult<ActivityLog>> PageAsync(
        IQueryable<ActivityLog> query, PagedRequest request, CancellationToken ct)
    {
        query = query.AsNoTracking().Include(a => a.Employee);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim();
            query = query.Where(a => a.Detail.Contains(keyword));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return new PagedResult<ActivityLog>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
