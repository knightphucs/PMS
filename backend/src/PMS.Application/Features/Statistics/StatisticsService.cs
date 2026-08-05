using PMS.Application.Common.Authorization;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Statistics;

public class StatisticsService : IStatisticsService
{
    private readonly IProjectStatisticsRepository _stats;
    private readonly IProjectAuthorizationService _authz;

    public StatisticsService(
        IProjectStatisticsRepository stats, IProjectAuthorizationService authz)
    {
        _stats = stats;
        _authz = authz;
    }

    public async Task<ProjectStatisticsResponse> GetProjectStatisticsAsync(
        Guid projectId, CancellationToken ct = default)
    {
        // ProjectAction.ViewStatistics — từ 2026-08-03 cả ba vai trò đều được (ADR-039).
        // Người ngoài project vẫn nhận 404 như mọi endpoint project-scoped khác.
        await _authz.AuthorizeAsync(projectId, ProjectAction.ViewStatistics, ct);

        var total = await _stats.CountTasksAsync(projectId, ct);
        var overdue = await _stats.CountOverdueAsync(projectId, ct);
        var byStatus = await _stats.TallyByStatusAsync(projectId, ct);
        var byPriority = await _stats.TallyByPriorityAsync(projectId, ct);
        var byAssignee = await _stats.TallyByAssigneeAsync(projectId, ct);
        var sprints = await _stats.TallyBySprintAsync(projectId, ct);

        // Cộng MỌI cột thuộc nhóm Done — sau ADR-052 một project có thể có nhiều cột kết
        // thúc ("Đã ship", "Hủy bỏ"), nên lấy đúng một cột là đếm thiếu.
        var done = byStatus.Where(s => s.Category == StatusCategory.Done).Sum(s => s.Count);

        var today = DateTime.UtcNow.Date;

        return new ProjectStatisticsResponse(
            projectId,
            total,
            overdue,
            total == 0 ? 0m : Math.Round((decimal)done / total * 100, 2),
            // KHÔNG ZeroFill: `TallyByStatusAsync` đã bắt đầu từ bảng cột nên cột rỗng
            // vốn đã có mặt với số 0. Danh mục cột nằm trong DB, không phải trong enum.
            byStatus
                .Select(s => new StatusCount(
                    s.ColumnId, s.Name, s.Color, s.Order, s.Category, s.Count))
                .ToList(),
            ZeroFill(byPriority, t => t.Priority, t => t.Count,
                (priority, count) => new PriorityCount(priority, count)),
            byAssignee
                .Select(a => new AssigneeWorkload(a.EmployeeId, a.EmployeeName, a.Total, a.Done, a.Overdue))
                .ToList(),
            sprints
                .Select(s => new SprintProgress(
                    s.SprintId, s.Name,
                    // IsActive tính ở đây, cùng công thức Sprint.IsActive của domain — không
                    // gọi property đó được vì truy vấn tổng hợp không nạp entity Sprint nào.
                    s.StartDate.Date <= today && today <= s.EndDate.Date,
                    s.StartDate, s.EndDate, s.Total, s.Done))
                .ToList());
    }

    /// <summary>
    /// Bù đủ mọi giá trị enum, kể cả giá trị không có task nào — cùng lý do
    /// <c>TaskService.GetBoardAsync</c> luôn dựng đủ 4 cột: biểu đồ ở frontend không nên
    /// phải tự bịa ra hạng mục còn thiếu, và nếu nó tự bịa thì bảng đó sẽ lệch dần khỏi
    /// backend mỗi lần thêm một giá trị enum mới.
    /// Thứ tự trả về theo thứ tự khai báo enum, ổn định giữa các lần gọi.
    /// </summary>
    private static IReadOnlyList<TResult> ZeroFill<TTally, TEnum, TResult>(
        IReadOnlyList<TTally> tallies,
        Func<TTally, TEnum> keyOf,
        Func<TTally, int> countOf,
        Func<TEnum, int, TResult> build) where TEnum : struct, Enum
    {
        var lookup = tallies.ToDictionary(keyOf, countOf);
        return Enum.GetValues<TEnum>()
            .Select(value => build(value, lookup.GetValueOrDefault(value)))
            .ToList();
    }
}
