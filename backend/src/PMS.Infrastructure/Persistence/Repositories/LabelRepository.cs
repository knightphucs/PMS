using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

public class LabelRepository : Repository<Label>, ILabelRepository
{
    public LabelRepository(PmsDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Label>> ListAllOrderedAsync(CancellationToken ct = default)
        => await DbSet.AsNoTracking().OrderBy(l => l.Name).ToListAsync(ct);

    public async Task<bool> NameExistsAsync(
        string name, Guid? excludingId = null, CancellationToken ct = default)
        => await DbSet.AnyAsync(
            l => l.Name == name && (excludingId == null || l.Id != excludingId), ct);

    public async Task<Label?> GetWithTasksAsync(Guid id, CancellationToken ct = default)
        => await DbSet.Include(l => l.Tasks).FirstOrDefaultAsync(l => l.Id == id, ct);
}
