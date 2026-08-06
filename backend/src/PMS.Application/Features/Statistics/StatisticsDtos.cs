using PMS.Domain.Enums;

namespace PMS.Application.Features.Statistics;

/// <summary>
/// Một cột trên biểu đồ phân bố trạng thái. Sau ADR-052 nó là MỘT CỘT BOARD chứ không phải
/// một giá trị enum, nên số phần tử thay đổi theo cấu hình của từng project.
/// </summary>
public record StatusCount(
    Guid ColumnId, string Name, string Color, int Order, StatusCategory Category, int Count);

public record PriorityCount(Priority Priority, int Count);

public record AssigneeWorkload(Guid EmployeeId, string EmployeeName, int Total, int Done, int Overdue);

public record SprintProgress(
    Guid SprintId, string Name, bool IsActive,
    DateTime StartDate, DateTime EndDate,
    int Total, int Done);

/// <summary>
/// Thống kê một project (ADR-039).
/// <para>
/// 📌 Đếm <b>cả subtask</b>: theo §5, subtask là một Task đầy đủ với Status/Assignee/DueDate
/// riêng, nên nó là công việc thật và phải vào thống kê. Board loại subtask ra chỉ vì lý do
/// hiển thị (mỗi subtask nằm trong chi tiết task cha), không phải vì nó không phải công việc.
/// </para>
/// </summary>
public record ProjectStatisticsResponse(
    Guid ProjectId,
    int TotalTasks,
    int OverdueTasks,
    /// <summary>Tỷ lệ task đã Done, 0–100, làm tròn 2 chữ số. Không có task nào thì bằng 0.</summary>
    decimal CompletionRate,
    /// <summary>Luôn đủ 4 phần tử, kể cả trạng thái không có task nào.</summary>
    IReadOnlyList<StatusCount> ByStatus,
    /// <summary>Luôn đủ 5 phần tử.</summary>
    IReadOnlyList<PriorityCount> ByPriority,
    IReadOnlyList<AssigneeWorkload> ByAssignee,
    IReadOnlyList<SprintProgress> Sprints);
