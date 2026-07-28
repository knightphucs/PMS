using PMS.Application.Common.Models;
using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface ITaskRepository : IRepository<TaskItem>
{
    Task<TaskItem?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<TaskItem?> GetWithSubtasksAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<TaskItem>> GetPagedByProjectAsync(
        Guid projectId, PagedRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetBacklogAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetBySprintAsync(Guid sprintId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetUnfinishedBlockersAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetOverdueAsync(CancellationToken ct = default);
    Task<int> CountActiveAssignedAsync(Guid projectId, Guid employeeId, CancellationToken ct = default);
}
