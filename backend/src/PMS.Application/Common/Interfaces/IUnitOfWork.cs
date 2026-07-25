namespace PMS.Application.Common.Interfaces;

public interface IUnitOfWork
{
    IProjectRepository Projects { get; }
    ITaskRepository Tasks { get; }
    IEmployeeRepository Employees { get; }
    IRefreshTokenRepository RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Chỉ dùng khi 1 nghiệp vụ cần NHIỀU lần SaveChanges mà vẫn phải nguyên tử.
    /// Nhận delegate thay vì trả về IDbContextTransaction để không rò rỉ kiểu của EF Core
    /// ra tầng Application (giữ Application độc lập hạ tầng).
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default);
}
