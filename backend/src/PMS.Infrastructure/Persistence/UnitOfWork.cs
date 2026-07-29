using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Infrastructure.Persistence.Repositories;

namespace PMS.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly PmsDbContext _context;

    private IProjectRepository? _projects;
    private ITaskRepository? _tasks;
    private IEmployeeRepository? _employees;
    private IRefreshTokenRepository ? _refreshTokens;
    private IRepository<Sprint>? _sprints;
    private IProjectMemberRepository? _projectMembers;
    private IRepository<ActivityLog>? _activityLogs;
    private IRepository<Notification>? _notifications;

    public UnitOfWork(PmsDbContext context) => _context = context;

    public IProjectRepository Projects  => _projects  ??= new ProjectRepository(_context);
    public ITaskRepository Tasks        => _tasks     ??= new TaskRepository(_context);
    public IEmployeeRepository Employees => _employees ??= new EmployeeRepository(_context);
    public IRefreshTokenRepository RefreshTokens => _refreshTokens ??= new RefreshTokenRepository(_context);
    public IRepository<Sprint> Sprints => _sprints ??= new Repository<Sprint>(_context);
    public IProjectMemberRepository ProjectMembers => _projectMembers ??= new ProjectMemberRepository(_context);
    public IRepository<ActivityLog> ActivityLogs => _activityLogs ??= new Repository<ActivityLog>(_context);
    public IRepository<Notification> Notifications => _notifications ??= new Repository<Notification>(_context);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(
        Func<Task> operation, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                await operation();
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }
}
