using PMS.Domain.Enums;

namespace PMS.Application.Features.Tasks;

public record CreateTaskRequest(
    string Name,
    Guid ProjectId,
    Guid? SprintId,
    Guid? ParentTaskId,
    DateTime? DueDate,
    Priority Priority);

public record UpdateTaskRequest(
    string Name,
    DateTime? DueDate,
    Priority Priority,
    byte[] RowVersion);

public record MoveTaskToSprintRequest(Guid? SprintId);

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

public record BoardColumn(Status Status, IReadOnlyList<TaskSummaryResponse> Tasks);

public record BoardResponse(Guid ProjectId, Guid? SprintId, IReadOnlyList<BoardColumn> Columns);

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
