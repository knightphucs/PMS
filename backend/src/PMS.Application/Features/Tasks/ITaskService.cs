using PMS.Application.Common.Models;

namespace PMS.Application.Features.Tasks;

public interface ITaskService
{
    Task<TaskSummaryResponse> CreateAsync(CreateTaskRequest request, CancellationToken ct = default);

    Task<TaskDetailResponse> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<TaskSummaryResponse>> GetByProjectAsync(
        Guid projectId, PagedRequest request, CancellationToken ct = default);

    Task<TaskDetailResponse> UpdateAsync(
        Guid id, UpdateTaskRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<TaskSummaryResponse> MoveToSprintAsync(
        Guid id, MoveTaskToSprintRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<TaskSummaryResponse>> GetBacklogAsync(
        Guid projectId, CancellationToken ct = default);

    Task<BoardResponse> GetBoardAsync(
        Guid projectId, Guid? sprintId, CancellationToken ct = default);
}
