using PMS.Domain.Enums;

namespace PMS.Application.Features.Tasks;

/// <summary>
/// ReporterId không nằm trong request: người tạo task chính là Reporter, lấy từ JWT
/// (seq-01). Cho client tự khai sẽ mở đường mạo danh người báo cáo.
/// </summary>
public record CreateTaskRequest(
    string Name,
    Guid ProjectId,
    Guid? SprintId,
    Guid? ParentTaskId,
    DateTime? DueDate,
    Priority Priority);

/// <summary>
/// Không có Status ở đây — đổi trạng thái đi qua endpoint riêng để bắt buộc chạy qua
/// Workflow Transition Rules, không sửa lén bằng một lệnh PUT (§5).
/// RowVersion bắt buộc theo ADR-016/021.
/// </summary>
public record UpdateTaskRequest(
    string Name,
    DateTime? DueDate,
    Priority Priority,
    byte[] RowVersion);

/// <summary>SprintId = null nghĩa là đưa task về Backlog.</summary>
public record MoveTaskToSprintRequest(Guid? SprintId);

/// <summary>Gán người khác vào task — chỉ ProjectManager gọi được (seq-02).</summary>
public record AssignTaskRequest(Guid EmployeeId, RoleInTask Role);

public record TaskSummaryResponse(
    Guid Id,
    string Name,
    Status Status,
    Priority Priority,
    DateTime? DueDate,
    bool IsOverdue,
    Guid? SprintId,
    Guid? ParentTaskId,
    decimal SubtaskProgress);

public record TaskAssigneeResponse(
    Guid EmployeeId,
    string EmployeeName,
    RoleInTask RoleInTask,
    DateTime AssignedDate);

public record TaskDetailResponse(
    Guid Id,
    string Name,
    Status Status,
    Priority Priority,
    DateTime? DueDate,
    bool IsOverdue,
    Guid ProjectId,
    Guid? SprintId,
    Guid? ParentTaskId,
    Guid ReporterId,
    string ReporterName,
    IReadOnlyList<TaskAssigneeResponse> Assignees,
    IReadOnlyList<TaskSummaryResponse> Subtasks,
    decimal SubtaskProgress,
    byte[] RowVersion);
