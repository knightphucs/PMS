using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Không cần lọc tay theo <c>Task.IsDeleted</c> ở bất kỳ method nào:
/// <c>CommentConfiguration</c> đã khai <c>HasQueryFilter(c =&gt; !c.Task.IsDeleted)</c> nên
/// comment của task đã xóa mềm tự biến mất khỏi MỌI query — bảo đảm bằng cấu trúc, không
/// phải bằng việc mỗi method phải nhớ (ADR-026).
/// </summary>
public class CommentRepository : Repository<Comment>, ICommentRepository
{
    public CommentRepository(PmsDbContext context) : base(context) { }

    public async Task<PagedResult<Comment>> GetPagedByTaskAsync(
        Guid taskId, PagedRequest request, CancellationToken ct = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Include(c => c.Author)
            .Where(c => c.TaskId == taskId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim();
            query = query.Where(c => c.Content.Contains(keyword));
        }

        var totalCount = await query.CountAsync(ct);

        query = request.IsDescending
            ? query.OrderByDescending(c => c.CreatedAt)
            : query.OrderBy(c => c.CreatedAt);   // mặc định: đọc hội thoại từ đầu

        var items = await query.Skip(request.Skip).Take(request.PageSize).ToListAsync(ct);
        return new PagedResult<Comment>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<Comment?> GetWithTaskAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(c => c.Task)
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
}
