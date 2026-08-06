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

    /// <summary>
    /// Việc của người đang gọi, gom theo dự án (ADR-053). KHÔNG nhận <c>employeeId</c> từ
    /// tham số: nó luôn là người đang đăng nhập. Cho phép truyền vào là mở một endpoint
    /// xem lịch làm việc của người khác mà không ai chủ ý thiết kế.
    /// </summary>
    Task<MyWorkResponse> GetMyWorkAsync(CancellationToken ct = default);

    Task<BoardResponse> GetBoardAsync(
        Guid projectId, Guid? sprintId, CancellationToken ct = default);
}
