using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface ISprintRepository : IRepository<Sprint>
{
    /// <summary>
    /// Sprint của 1 project, nạp kèm Tasks để tính TaskCount. Số sprint mỗi project
    /// nhỏ nên Include chấp nhận được; nếu sau này phình to thì đổi sang projection.
    /// </summary>
    Task<IReadOnlyList<Sprint>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Nạp kèm Tasks có tracking — bắt buộc cho việc xóa sprint, vì phải đẩy từng task
    /// về Backlog (SprintId = null) trước khi xóa mềm sprint (ADR-020).
    /// </summary>
    Task<Sprint?> GetWithTasksAsync(Guid id, CancellationToken ct = default);
}
