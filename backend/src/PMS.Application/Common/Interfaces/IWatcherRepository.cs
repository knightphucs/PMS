using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

/// <summary>
/// 🔴 <b>KHÔNG</b> kế thừa <see cref="IRepository{T}"/>: ràng buộc của nó là
/// <c>where T : BaseEntity</c>, mà <see cref="Watcher"/> dùng khóa kép
/// <c>(TaskId, EmployeeId)</c> và không có cột <c>Id</c> (ADR-036).
/// <para>
/// 🔴 Hệ quả thứ hai, dễ bỏ sót hơn: <c>PmsDbContext.ApplyAuditFields()</c> duyệt
/// <c>ChangeTracker.Entries&lt;BaseEntity&gt;()</c>, nên <c>Watcher.CreatedAt</c>
/// <b>không</b> được đóng dấu tự động và cũng không có default value ở
/// <c>WatcherConfiguration</c>. Người tạo <c>Watcher</c> phải tự set, nếu không nó là
/// <c>0001-01-01</c>.
/// </para>
/// </summary>
public interface IWatcherRepository
{
    Task<Watcher?> GetAsync(Guid taskId, Guid employeeId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid taskId, Guid employeeId, CancellationToken ct = default);

    /// <summary>Nạp kèm <c>Employee</c> — response cần tên người theo dõi.</summary>
    Task<IReadOnlyList<Watcher>> ListByTaskAsync(Guid taskId, CancellationToken ct = default);

    void Add(Watcher watcher);
    void Remove(Watcher watcher);
}
