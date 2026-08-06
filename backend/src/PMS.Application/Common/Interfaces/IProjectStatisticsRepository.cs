using PMS.Domain.Enums;

namespace PMS.Application.Common.Interfaces;

/// <summary>
/// Kết quả tổng hợp thô, đã do SQL <c>GROUP BY</c> tính — tầng Application chỉ zero-fill và
/// đổi tên. Tách riêng khỏi các repository CRUD vì đây thuần là truy vấn đọc/tổng hợp,
/// không phục vụ vòng đời entity nào.
/// </summary>
/// <summary>
/// Đếm task theo CỘT (ADR-052) — trước đây là theo enum <c>Status</c>.
/// Mang theo tên và màu vì biểu đồ phải vẽ đúng cột người dùng đã đặt, và
/// <c>Category</c> để tính tỉ lệ hoàn thành mà không phải đoán từ tên.
/// </summary>
public record StatusTally(
    Guid ColumnId, string Name, string Color, int Order, StatusCategory Category, int Count);
public record PriorityTally(Priority Priority, int Count);
public record AssigneeTally(Guid EmployeeId, string EmployeeName, int Total, int Done, int Overdue);
public record SprintTally(
    Guid SprintId, string Name, DateTime StartDate, DateTime EndDate, int Total, int Done);

/// <summary>Một hàng của `sp_GetProjectBacklogInsight` (migration AddReportingDbObjects).</summary>
public record BacklogInsightTally(int TotalOpen, int Overdue, int DueSoon, int NoDueDate);

/// <summary>Một hàng của view `vw_SprintVelocity` — chỉ sprint đã ĐÓNG SỔ mới xuất hiện.</summary>
public record SprintVelocityTally(Guid SprintId, string Name, DateTime CompletedAt, int Total, int Done);

/// <summary>
/// Một hàng của timeline — KHÁC <see cref="SprintTally"/>: mang theo <see cref="Status"/> và
/// <c>CompletedAt</c> thật (do người dùng bấm), không suy diễn "đang chạy" từ ngày như
/// <c>SprintProgress.IsActive</c> của Thống kê. Timeline vẽ MỌI sprint (kể cả Planned) trên
/// một trục thời gian chung, nên cần biết đúng trạng thái vòng đời để tô màu, không chỉ có
/// hay không có task.
/// </summary>
public record SprintTimelineTally(
    Guid SprintId, string Name, SprintStatus Status,
    DateTime StartDate, DateTime EndDate, DateTime? CompletedAt, int Total, int Done);

public interface IProjectStatisticsRepository
{
    /// <summary>
    /// 🔴 Mọi method ở đây phải tổng hợp <b>trong SQL</b>, không nạp entity rồi đếm trong
    /// bộ nhớ: một project 5 000 task không được materialize chỉ để vẽ một biểu đồ tròn.
    /// </summary>
    Task<int> CountTasksAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// ⚠️ KHÔNG dùng được <c>TaskItem.IsOverdue</c>: đó là computed property của C#, EF không
    /// dịch được sang SQL và sẽ ném lúc chạy. Phải viết lại điều kiện dưới dạng biểu thức
    /// dịch được — soi gương đúng dạng của <c>ITaskRepository.GetOverdueAsync</c>.
    /// </summary>
    Task<int> CountOverdueAsync(Guid projectId, CancellationToken ct = default);

    Task<IReadOnlyList<StatusTally>> TallyByStatusAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<PriorityTally>> TallyByPriorityAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<AssigneeTally>> TallyByAssigneeAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<SprintTally>> TallyBySprintAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Nhóm báo cáo kiểu Jira — backlog insight (§1 hạng mục 11 ARCHITECTURE.md). Gọi
    /// <c>sp_GetProjectBacklogInsight</c> — xem chú thích ở nơi cài đặt vì sao qua stored
    /// procedure chứ không LINQ.
    /// </summary>
    Task<BacklogInsightTally> GetBacklogInsightAsync(
        Guid projectId, int dueSoonHorizonDays, CancellationToken ct = default);

    /// <summary>Phần bổ trợ theo priority của backlog insight — <c>sp_GetProjectBacklogByPriority</c>.</summary>
    Task<IReadOnlyList<PriorityTally>> GetBacklogByPriorityAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Velocity — CHỈ sprint đã <c>Completed</c> (qua view <c>vw_SprintVelocity</c>). Khác
    /// <see cref="TallyBySprintAsync"/> (mọi sprint, phục vụ tab Thống kê): velocity không có
    /// ý nghĩa gì với sprint chưa đóng sổ.
    /// </summary>
    Task<IReadOnlyList<SprintVelocityTally>> TallyVelocityAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Timeline — MỌI sprint (Planned/Active/Completed), sắp theo <c>StartDate</c>. Khác
    /// <see cref="TallyBySprintAsync"/> chỉ ở chỗ mang theo <see cref="SprintTimelineTally.Status"/>
    /// và <c>CompletedAt</c> thật, cần cho việc tô màu theo vòng đời thay vì suy từ ngày.
    /// </summary>
    Task<IReadOnlyList<SprintTimelineTally>> TallyTimelineAsync(
        Guid projectId, CancellationToken ct = default);
}
