using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IUnitOfWork
{
    IProjectRepository Projects { get; }
    ITaskRepository Tasks { get; }
    IBoardColumnRepository BoardColumns { get; }
    IEmployeeRepository Employees { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    ISprintRepository Sprints { get; }
    IProjectMemberRepository ProjectMembers { get; }
    INotificationRepository Notifications { get; }
    ICommentRepository Comments { get; }
    IRepository<TaskAssignment> TaskAssignments { get; }
    IActivityLogRepository ActivityLogs { get; }
    IProjectTaskCounterRepository ProjectTaskCounters { get; }
    ILabelRepository Labels { get; }
    IWatcherRepository Watchers { get; }
    ITaskLinkRepository TaskLinks { get; }
    IAttachmentRepository Attachments { get; }
    IPasswordResetTokenRepository PasswordResetTokens { get; }
    IPermissionRepository Permissions { get; }
    IProjectInvitationRepository ProjectInvitations { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Chỉ dùng khi 1 nghiệp vụ cần NHIỀU lần SaveChanges mà vẫn phải nguyên tử.
    /// Nhận delegate thay vì trả về IDbContextTransaction để không rò rỉ kiểu của EF Core
    /// ra tầng Application (giữ Application độc lập hạ tầng).
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default);

    /// <summary>
    /// Ghi đè original value của cột RowVersion trên 1 entity đã tracked, bằng token client
    /// gửi lên (đọc được từ response GET trước đó). Bắt buộc phải gọi trước SaveChangesAsync
    /// để optimistic concurrency check so với đúng version client đang thấy, thay vì so với
    /// version vừa load trong cùng request (luôn khớp, nên không bao giờ conflict).
    /// </summary>
    void SetConcurrencyToken<TEntity>(TEntity entity, byte[] rowVersion) where TEntity : class;
}
