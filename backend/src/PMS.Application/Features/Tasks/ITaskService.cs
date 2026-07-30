using PMS.Application.Common.Models;

namespace PMS.Application.Features.Tasks;

public interface ITaskService
{
    /// <summary>Tạo task hoặc subtask (khi có ParentTaskId) — seq-01.</summary>
    Task<TaskSummaryResponse> CreateAsync(CreateTaskRequest request, CancellationToken ct = default);

    Task<TaskDetailResponse> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Danh sách task gốc của project (subtask hiện trong chi tiết task cha).</summary>
    Task<PagedResult<TaskSummaryResponse>> GetByProjectAsync(
        Guid projectId, PagedRequest request, CancellationToken ct = default);

    Task<TaskDetailResponse> UpdateAsync(
        Guid id, UpdateTaskRequest request, CancellationToken ct = default);

    /// <summary>Xóa mềm task; chặn 409 nếu còn subtask chưa Done (ADR-018).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Chuyển task giữa Sprint và Backlog (SprintId = null).</summary>
    Task<TaskSummaryResponse> MoveToSprintAsync(
        Guid id, MoveTaskToSprintRequest request, CancellationToken ct = default);

    /// <summary>Task chưa gán sprint của project (Backlog).</summary>
    Task<IReadOnlyList<TaskSummaryResponse>> GetBacklogAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Board Kanban: task nhóm theo Status. sprintId = null nghĩa là board của cả project.
    /// </summary>
    Task<BoardResponse> GetBoardAsync(
        Guid projectId, Guid? sprintId, CancellationToken ct = default);
}
