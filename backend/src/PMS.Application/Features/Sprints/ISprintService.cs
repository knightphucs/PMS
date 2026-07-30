namespace PMS.Application.Features.Sprints;

public interface ISprintService
{
    Task<SprintResponse> CreateAsync(
        Guid projectId, CreateSprintRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<SprintResponse>> GetByProjectAsync(
        Guid projectId, CancellationToken ct = default);

    Task<SprintResponse> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<SprintResponse> UpdateAsync(
        Guid id, UpdateSprintRequest request, CancellationToken ct = default);

    /// <summary>Xóa mềm sprint và đẩy toàn bộ task của nó về Backlog (ADR-020).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
