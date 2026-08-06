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
    private IBoardColumnRepository? _boardColumns;
    private IProjectMemberRepository? _projectMembers;
    private IRepository<TaskAssignment>? _taskAssignments;
    private IActivityLogRepository? _activityLogs;
    private ILabelRepository? _labels;
    private IWatcherRepository? _watchers;
    private ITaskLinkRepository? _taskLinks;
    private IAttachmentRepository? _attachments;
    private IPasswordResetTokenRepository? _passwordResetTokens;
    private INotificationRepository? _notifications;
    private ICommentRepository? _comments;
    private IProjectTaskCounterRepository? _projectTaskCounters;
    private IPermissionRepository? _permissions;

    public UnitOfWork(PmsDbContext context) => _context = context;

    public IProjectRepository Projects  => _projects  ??= new ProjectRepository(_context);
    public ITaskRepository Tasks        => _tasks     ??= new TaskRepository(_context);
    public IBoardColumnRepository BoardColumns => _boardColumns ??= new BoardColumnRepository(_context);
    public IEmployeeRepository Employees => _employees ??= new EmployeeRepository(_context);
    public IRefreshTokenRepository RefreshTokens => _refreshTokens ??= new RefreshTokenRepository(_context);
    public ISprintRepository Sprints => _sprints ??= new SprintRepository(_context);
    public IProjectMemberRepository ProjectMembers => _projectMembers ??= new ProjectMemberRepository(_context);
    public IRepository<TaskAssignment> TaskAssignments => _taskAssignments ??= new Repository<TaskAssignment>(_context);
    public IActivityLogRepository ActivityLogs => _activityLogs ??= new ActivityLogRepository(_context);
    public ILabelRepository Labels => _labels ??= new LabelRepository(_context);
    public IWatcherRepository Watchers => _watchers ??= new WatcherRepository(_context);
    public ITaskLinkRepository TaskLinks => _taskLinks ??= new TaskLinkRepository(_context);
    public IAttachmentRepository Attachments => _attachments ??= new AttachmentRepository(_context);
    public IPasswordResetTokenRepository PasswordResetTokens => _passwordResetTokens ??= new PasswordResetTokenRepository(_context);
    public INotificationRepository Notifications => _notifications ??= new NotificationRepository(_context);
    public ICommentRepository Comments => _comments ??= new CommentRepository(_context);
    public IProjectTaskCounterRepository ProjectTaskCounters => _projectTaskCounters ??= new ProjectTaskCounterRepository(_context);
    public IPermissionRepository Permissions => _permissions ??= new PermissionRepository(_context);

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
