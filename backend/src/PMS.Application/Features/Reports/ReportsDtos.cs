using PMS.Application.Features.Statistics;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Reports;

/// <summary>
/// Backlog insight — nhóm báo cáo kiểu Jira (§1 hạng mục 11 ARCHITECTURE.md).
/// <c>ByPriority</c> tái dùng <see cref="PriorityCount"/> của Thống kê thay vì định nghĩa
/// lại: cùng hình dạng, cùng ý nghĩa (số task theo mức ưu tiên), khác chỉ ở tập task được
/// đếm (đây là "còn mở", Thống kê là "toàn bộ").
/// </summary>
public record BacklogInsightResponse(
    Guid ProjectId,
    int TotalOpen,
    int Overdue,
    int DueSoon,
    int NoDueDate,
    IReadOnlyList<PriorityCount> ByPriority);

/// <summary>Một điểm trên biểu đồ velocity — một sprint đã đóng sổ.</summary>
public record SprintVelocityPoint(
    Guid SprintId, string Name, DateTime CompletedAt, int DoneCount, int TotalCount);

/// <summary>
/// Velocity — CHỈ tính sprint đã <c>Completed</c>. <c>AverageVelocity</c> là số task Done
/// trung bình mỗi sprint đã đóng; <c>0</c> khi chưa có sprint nào đóng (không chia 0).
/// </summary>
public record VelocityResponse(
    Guid ProjectId, IReadOnlyList<SprintVelocityPoint> Sprints, decimal AverageVelocity);

/// <summary>
/// Một sprint trên trục thời gian — MỌI vòng đời đều có mặt, khác <see cref="SprintVelocityPoint"/>
/// chỉ có sprint đã đóng. <c>CompletedAt</c> là mốc đóng sổ THẬT (null nếu chưa đóng); client
/// dùng <see cref="Status"/> để tô màu, không suy diễn "đang chạy" từ <c>StartDate</c>/<c>EndDate</c>.
/// </summary>
public record SprintTimelinePoint(
    Guid SprintId, string Name, SprintStatus Status,
    DateTime StartDate, DateTime EndDate, DateTime? CompletedAt, int Total, int Done,
    /// <summary>
    /// Quá hạn THẬT — <c>Active</c> và đã qua <c>EndDate</c> mà chưa đóng sổ. Tính lại ở
    /// đây (không gọi <c>Sprint.IsOverdue</c>) vì <c>TallyTimelineAsync</c> chỉ trả về một
    /// tally phẳng, không nạp entity <c>Sprint</c> nào — cùng lý do <c>StatisticsService</c>
    /// tự tính <c>IsActive</c> thay vì đọc property của entity.
    /// </summary>
    bool IsOverdue);

/// <summary>Timeline kiểu Jira roadmap — mọi sprint của project, sắp theo ngày bắt đầu.</summary>
public record TimelineResponse(Guid ProjectId, IReadOnlyList<SprintTimelinePoint> Sprints);
