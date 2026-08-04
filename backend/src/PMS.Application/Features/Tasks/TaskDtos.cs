using PMS.Application.Features.Labels;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Tasks;

public record CreateTaskRequest(
    string Name,
    Guid ProjectId,
    Guid? SprintId,
    Guid? ParentTaskId,
    DateTime? DueDate,
    Priority Priority,
    string? Description = null);

public record UpdateTaskRequest(
    string Name,
    DateTime? DueDate,
    Priority Priority,
    byte[] RowVersion,
    string? Description = null);

public record MoveTaskToSprintRequest(Guid? SprintId);

public record AssignTaskRequest(Guid EmployeeId, RoleInTask Role);

/// <summary>
/// Người đảm nhận, rút gọn cho THẺ trên board/backlog — chỉ đủ để vẽ avatar.
/// Khác <see cref="TaskAssigneeResponse"/> ở chỗ bỏ <c>RoleInTask</c> và
/// <c>AssignedDate</c>: board trả về hàng chục task một lượt, mỗi byte thừa nhân lên
/// theo số thẻ. Cần đầy đủ thì gọi <c>GET /tasks/{id}/assignees</c>.
/// </summary>
public record TaskCardAssignee(Guid EmployeeId, string EmployeeName);

public record TaskSummaryResponse(
    Guid Id,
    /// <summary>Số thứ tự trong project. Cần khi muốn sắp xếp hoặc tra cứu bằng số.</summary>
    int Number,
    /// <summary>
    /// Mã hiển thị đã ghép sẵn, dạng <c>PMS-12</c>. Backend ghép chứ không trả rời
    /// <c>ProjectKey</c> + <c>Number</c> để frontend tự nối: hai nơi định dạng thì chắc
    /// chắn có lúc lệch nhau (ADR-034).
    /// </summary>
    string Code,
    string Name,
    Status Status,
    Priority Priority,
    DateTime? DueDate,
    bool IsOverdue,
    Guid? SprintId,
    Guid? ParentTaskId,
    decimal SubtaskProgress,
    IReadOnlyList<TaskCardAssignee> Assignees,
    IReadOnlyList<LabelResponse> Labels);

public record TaskAssigneeResponse(
    Guid EmployeeId,
    string EmployeeName,
    RoleInTask RoleInTask,
    DateTime AssignedDate);

public record BoardColumn(Status Status, IReadOnlyList<TaskSummaryResponse> Tasks);

public record BoardResponse(Guid ProjectId, Guid? SprintId, IReadOnlyList<BoardColumn> Columns);

public record TaskDetailResponse(
    Guid Id,
    int Number,
    string Code,
    string Name,
    string? Description,
    Status Status,
    Priority Priority,
    DateTime? DueDate,
    bool IsOverdue,
    Guid ProjectId,
    string ProjectKey,
    Guid? SprintId,
    Guid? ParentTaskId,
    Guid ReporterId,
    string ReporterName,
    IReadOnlyList<TaskAssigneeResponse> Assignees,
    IReadOnlyList<TaskSummaryResponse> Subtasks,
    IReadOnlyList<LabelResponse> Labels,
    /// <summary>
    /// Người gọi có đang theo dõi task này không — phụ thuộc NGƯỜI HỎI nên không suy ra
    /// được từ entity, phải truyền employeeId vào mapper (ADR-036).
    /// </summary>
    bool IsWatching,
    decimal SubtaskProgress,
    byte[] RowVersion);
