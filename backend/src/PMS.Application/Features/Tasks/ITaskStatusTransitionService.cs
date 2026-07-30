using PMS.Domain.Enums;

namespace PMS.Application.Features.Tasks;

/// <summary>Target là trạng thái muốn chuyển tới, không phải trạng thái hiện tại.</summary>
public record ChangeTaskStatusRequest(Status Target);

/// <summary>
/// Tách khỏi ITaskService vì luật đổi trạng thái cần dữ liệu mà cả domain lẫn
/// IProjectAuthorizationService đều không tự có: vừa cần RoleInProject (từ ProjectMember),
/// vừa cần danh sách assignee (từ TaskAssignment) — ADR-017.
/// </summary>
public interface ITaskStatusTransitionService
{
    Task<TaskSummaryResponse> ChangeStatusAsync(
        Guid taskId, ChangeTaskStatusRequest request, CancellationToken ct = default);
}
