using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Infrastructure.Persistence.Repositories;

public class NotificationRepository : Repository<Notification>, INotificationRepository
{
    public NotificationRepository(PmsDbContext context) : base(context) { }

    public async Task<Notification?> GetForRecipientAsync(
        Guid id, Guid employeeId, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(n => n.Id == id && n.EmployeeId == employeeId, ct);

    public async Task<PagedResult<Notification>> GetPagedForRecipientAsync(
        Guid employeeId, bool? isRead, PagedRequest request, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().Where(n => n.EmployeeId == employeeId);

        if (isRead is { } read)
            query = query.Where(n => n.IsRead == read);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim();
            query = query.Where(n => n.Content.Contains(keyword));
        }

        var totalCount = await query.CountAsync(ct);

        var ordered = (request.SortBy?.ToLowerInvariant(), request.IsDescending) switch
        {
            ("createdat", false) => query.OrderBy(n => n.CreatedAt),
            ("isread", false)    => query.OrderBy(n => n.IsRead).ThenByDescending(n => n.CreatedAt),
            ("isread", true)     => query.OrderByDescending(n => n.IsRead).ThenByDescending(n => n.CreatedAt),
            _                    => query.OrderByDescending(n => n.CreatedAt)
        };

        // Tie-break theo Id: thiếu nó thì hai bản ghi có cùng khóa sắp xếp (cùng tên, cùng
        // CreatedAt, cùng trạng thái) có thứ tự KHÔNG xác định giữa hai truy vấn, nên phân
        // trang có thể trả trùng một dòng ở trang này và bỏ sót nó ở trang kia. Lỗi chỉ lộ
        // khi dữ liệu đủ nhiều và đúng lúc — tức là ở production chứ không phải lúc dev.
        query = ordered.ThenBy(n => n.Id);

        var items = await query.Skip(request.Skip).Take(request.PageSize).ToListAsync(ct);
        return new PagedResult<Notification>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<int> CountUnreadAsync(Guid employeeId, CancellationToken ct = default)
        => await DbSet.CountAsync(n => n.EmployeeId == employeeId && !n.IsRead, ct);

    public async Task<IReadOnlyList<Notification>> GetUnreadForRecipientAsync(
        Guid employeeId, CancellationToken ct = default)
        => await DbSet
            .Where(n => n.EmployeeId == employeeId && !n.IsRead)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<(Guid EmployeeId, Guid RelatedEntityId)>>
        GetNotifiedPairsSinceAsync(
            NotificationType type, DateTime since, IReadOnlyCollection<Guid> relatedEntityIds,
            CancellationToken ct = default)
    {
        // Một query duy nhất cho toàn bộ tập ứng viên, không phải mỗi cặp một query.
        var rows = await DbSet
            .AsNoTracking()
            .Where(n => n.Type == type
                     && n.CreatedAt >= since
                     && n.RelatedEntityId != null
                     && relatedEntityIds.Contains(n.RelatedEntityId.Value))
            .Select(n => new { n.EmployeeId, RelatedEntityId = n.RelatedEntityId!.Value })
            .Distinct()
            .ToListAsync(ct);

        return rows.Select(r => (r.EmployeeId, r.RelatedEntityId)).ToList();
    }
}
