using PMS.Domain.Common;

namespace PMS.Application.Common.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);
    void Add(T entity);
    void Update(T entity);
    void Remove(T entity);
}
