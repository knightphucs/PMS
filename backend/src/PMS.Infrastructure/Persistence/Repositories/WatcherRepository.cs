using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

public class WatcherRepository : IWatcherRepository
{
    private readonly PmsDbContext _context;

    public WatcherRepository(PmsDbContext context) => _context = context;

    public async Task<Watcher?> GetAsync(Guid taskId, Guid employeeId, CancellationToken ct = default)
        => await _context.Watchers
            .FirstOrDefaultAsync(w => w.TaskId == taskId && w.EmployeeId == employeeId, ct);

    public async Task<bool> ExistsAsync(Guid taskId, Guid employeeId, CancellationToken ct = default)
        => await _context.Watchers
            .AnyAsync(w => w.TaskId == taskId && w.EmployeeId == employeeId, ct);

    public async Task<IReadOnlyList<Watcher>> ListByTaskAsync(
        Guid taskId, CancellationToken ct = default)
        => await _context.Watchers
            .AsNoTracking()
            .Include(w => w.Employee)
            .Where(w => w.TaskId == taskId)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(ct);

    public void Add(Watcher watcher) => _context.Watchers.Add(watcher);

    public void Remove(Watcher watcher) => _context.Watchers.Remove(watcher);
}
