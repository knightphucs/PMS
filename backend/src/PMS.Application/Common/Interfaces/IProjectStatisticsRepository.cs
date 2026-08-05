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
}
