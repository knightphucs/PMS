using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

public class SprintRepository : Repository<Sprint>, ISprintRepository
{
    public SprintRepository(PmsDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Sprint>> GetByProjectAsync(
        Guid projectId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            .Include(s => s.Tasks)
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.StartDate)
            .AsSplitQuery()
            .ToListAsync(ct);

    public async Task<Sprint?> GetWithTasksAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(s => s.Tasks)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
}
