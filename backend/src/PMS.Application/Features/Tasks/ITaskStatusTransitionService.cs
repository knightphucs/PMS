namespace PMS.Application.Features.Tasks;

/// <summary>
/// Chuyển task sang một cột khác.
///
/// <para>
/// ⚠️ Trường đổi từ <c>Status Target</c> (enum) sang <c>Guid TargetColumnId</c> theo
/// ADR-052 — cột nay là dữ liệu của từng project, không còn là danh mục cố định của hệ
/// thống. Tên endpoint <c>PATCH /tasks/{id}/status</c> giữ nguyên vì nghiệp vụ không đổi.
/// </para>
/// </summary>
public record ChangeTaskStatusRequest(Guid TargetColumnId);

public interface ITaskStatusTransitionService
{
    Task<TaskSummaryResponse> ChangeStatusAsync(
        Guid taskId, ChangeTaskStatusRequest request, CancellationToken ct = default);
}
