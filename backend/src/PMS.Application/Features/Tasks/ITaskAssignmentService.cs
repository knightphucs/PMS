namespace PMS.Application.Features.Tasks;

public interface ITaskAssignmentService
{
    Task<IReadOnlyList<TaskAssigneeResponse>> GetAssigneesAsync(
        Guid taskId, CancellationToken ct = default);

    /// <summary>Gán người khác — chỉ ProjectManager (seq-02).</summary>
    Task<TaskAssigneeResponse> AssignAsync(
        Guid taskId, AssignTaskRequest request, CancellationToken ct = default);

    /// <summary>Tự nhận task — Member/PM, chỉ khi task đang ToDo (§5).</summary>
    Task<TaskAssigneeResponse> SelfAssignAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Tự rút khỏi task, hoặc PM gỡ người khác.</summary>
    Task UnassignAsync(Guid taskId, Guid employeeId, CancellationToken ct = default);
}
