using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Common.Interfaces;

/// <summary>Một cạnh của đồ thị "A chặn B" — chỉ hai đầu, đủ cho thuật toán dò chu trình.</summary>
public record BlockingEdge(Guid SourceTaskId, Guid TargetTaskId);

public interface ITaskLinkRepository : IRepository<TaskLink>
{
    /// <summary>Mọi liên kết có task này ở một trong hai đầu, kèm task đối diện + Project (để ghép mã).</summary>
    Task<IReadOnlyList<TaskLink>> ListByTaskAsync(Guid taskId, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid sourceTaskId, Guid targetTaskId, LinkType linkType, CancellationToken ct = default);

    /// <summary>
    /// Toàn bộ cạnh <see cref="LinkType.Blocks"/> trong một project — đầu vào cho phép dò
    /// chu trình trong bộ nhớ. Một project có vài trăm cạnh là cùng, nên không cần
    /// recursive CTE (ADR-038).
    /// </summary>
    Task<IReadOnlyList<BlockingEdge>> GetBlockingEdgesAsync(Guid projectId, CancellationToken ct = default);

    Task<TaskLink?> GetWithTasksAsync(Guid id, CancellationToken ct = default);
}
