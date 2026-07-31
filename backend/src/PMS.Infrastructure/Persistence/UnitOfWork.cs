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
    private ISprintRepository? _sprints;
    private IProjectMemberRepository? _projectMembers;
    private IRepository<TaskAssignment>? _taskAssignments;
    private IRepository<ActivityLog>? _activityLogs;
    private INotificationRepository? _notifications;
    private ICommentRepository? _comments;

    public UnitOfWork(PmsDbContext context) => _context = context;

    public IProjectRepository Projects  => _projects  ??= new ProjectRepository(_context);
    public ITaskRepository Tasks        => _tasks     ??= new TaskRepository(_context);
    public IEmployeeRepository Employees => _employees ??= new EmployeeRepository(_context);
    public IRefreshTokenRepository RefreshTokens => _refreshTokens ??= new RefreshTokenRepository(_context);
    public ISprintRepository Sprints => _sprints ??= new SprintRepository(_context);
    public IProjectMemberRepository ProjectMembers => _projectMembers ??= new ProjectMemberRepository(_context);
    public IRepository<TaskAssignment> TaskAssignments => _taskAssignments ??= new Repository<TaskAssignment>(_context);
    public IRepository<ActivityLog> ActivityLogs => _activityLogs ??= new Repository<ActivityLog>(_context);
    public INotificationRepository Notifications => _notifications ??= new NotificationRepository(_context);
    public ICommentRepository Comments => _comments ??= new CommentRepository(_context);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);

    public void SetConcurrencyToken<TEntity>(TEntity entity, byte[] rowVersion) where TEntity : class
        => _context.Entry(entity).Property("RowVersion").OriginalValue = rowVersion;

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
