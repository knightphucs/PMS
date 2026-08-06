using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

public class BoardColumnRepository : Repository<BoardColumn>, IBoardColumnRepository
{
    public BoardColumnRepository(PmsDbContext context) : base(context) { }

    public async Task<IReadOnlyList<BoardColumn>> ListByProjectAsync(
        Guid projectId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            .Where(c => c.ProjectId == projectId)
            // Tie-break bằng Id: Order KHÔNG unique (đổi thứ tự là ghi lại cả dải, bước
            // trung gian chắc chắn có trùng), nên thiếu nó thì hai cột cùng Order có thứ tự
            // không xác định giữa hai lần gọi — board sẽ tự nhảy cột mà không ai đụng gì.
            .OrderBy(c => c.Order).ThenBy(c => c.Id)
            .ToListAsync(ct);

    public async Task<BoardColumn?> GetDefaultForProjectAsync(
        Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.Order).ThenBy(c => c.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, int>> CountTasksByColumnAsync(
        Guid projectId, CancellationToken ct = default)
        => await Context.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .GroupBy(t => t.BoardColumnId)
            .Select(g => new { ColumnId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ColumnId, x => x.Count, ct);

    public async Task<int> MoveAllTasksAsync(
        Guid fromColumnId, BoardColumn target, CancellationToken ct = default)
        // ExecuteUpdateAsync: một lệnh UPDATE thẳng xuống DB, không tải entity nào lên bộ nhớ.
        // ⚠️ Nó KHÔNG đi qua ChangeTracker nên `TaskItem.MoveTo` không chạy — đó chính là lý
        // do phải đặt cả hai cột ở đây bằng tay. Quên `Category` thì bản sao trên task trôi
        // khỏi cột và mọi phép kiểm "xong chưa" trả lời sai, im lặng.
        => await Context.Tasks
            .Where(t => t.BoardColumnId == fromColumnId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.BoardColumnId, target.Id)
                .SetProperty(t => t.Category, target.Category), ct);

    public async Task<int> SyncTaskCategoriesAsync(BoardColumn column, CancellationToken ct = default)
        => await Context.Tasks
            .Where(t => t.BoardColumnId == column.Id && t.Category != column.Category)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Category, column.Category), ct);
}
