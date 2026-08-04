using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Infrastructure.Persistence.Repositories;

public class TaskLinkRepository : Repository<TaskLink>, ITaskLinkRepository
{
    public TaskLinkRepository(PmsDbContext context) : base(context) { }

    public async Task<IReadOnlyList<TaskLink>> ListByTaskAsync(
        Guid taskId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            // Project ở cả hai đầu: response ghép mã PMS-12 của task đối diện, mà mã cần
            // Project.Key (ADR-034). Đều là reference include nên không nhân dòng.
            .Include(l => l.SourceTask).ThenInclude(t => t.Project)
            .Include(l => l.TargetTask).ThenInclude(t => t.Project)
            .Where(l => l.SourceTaskId == taskId || l.TargetTaskId == taskId)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(
        Guid sourceTaskId, Guid targetTaskId, LinkType linkType, CancellationToken ct = default)
        => await DbSet.AnyAsync(
            l => l.SourceTaskId == sourceTaskId
              && l.TargetTaskId == targetTaskId
              && l.LinkType == linkType, ct);

    public async Task<IReadOnlyList<BlockingEdge>> GetBlockingEdgesAsync(
        Guid projectId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            // Chỉ Blocks: sau chuẩn hóa lúc ghi thì IsBlockedBy KHÔNG BAO GIỜ được lưu,
            // nên đồ thị chặn nằm gọn trong một loại cạnh duy nhất (ADR-038).
            .Where(l => l.LinkType == LinkType.Blocks && l.SourceTask.ProjectId == projectId)
            .Select(l => new BlockingEdge(l.SourceTaskId, l.TargetTaskId))
            .ToListAsync(ct);

    public async Task<TaskLink?> GetWithTasksAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(l => l.SourceTask)
            .Include(l => l.TargetTask)
            .FirstOrDefaultAsync(l => l.Id == id, ct);
}
