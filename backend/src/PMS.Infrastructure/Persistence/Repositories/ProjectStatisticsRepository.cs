using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Enums;

namespace PMS.Infrastructure.Persistence.Repositories;

public class ProjectStatisticsRepository : IProjectStatisticsRepository
{
    private readonly PmsDbContext _context;

    public ProjectStatisticsRepository(PmsDbContext context) => _context = context;

    /// <summary>
    /// Task của project, đã qua global query filter (task xóa mềm tự biến mất).
    /// Gồm CẢ subtask — subtask là công việc thật (§5), board loại nó ra chỉ vì hiển thị.
    /// </summary>
    private IQueryable<Domain.Entities.TaskItem> TasksOf(Guid projectId)
        => _context.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId);

    public Task<int> CountTasksAsync(Guid projectId, CancellationToken ct = default)
        => TasksOf(projectId).CountAsync(ct);

    public Task<int> CountOverdueAsync(Guid projectId, CancellationToken ct = default)
    {
        // ⚠️ KHÔNG viết `.Where(t => t.IsOverdue)`: IsOverdue là property computed của C#,
        // EF không dịch được và sẽ ném InvalidOperationException lúc chạy. Đây là biểu thức
        // tương đương, dịch được — giống hệt dạng TaskRepository.GetOverdueAsync đang dùng.
        var today = DateTime.UtcNow.Date;
        // ⚠️ So sánh THẲNG với mốc nửa đêm, KHÔNG dùng `.Value.Date`. Từ 2026-08-04 mọi cột
        // DateTime có ValueConverter (đóng dấu Kind=Utc lúc đọc), và EF **không dịch được
        // member access `.Date` trên cột đã chuyển đổi** — nó ném ngay lúc chạy, thành HTTP
        // 500. Hai vế tương đương về mặt toán học vì `today`/`horizon` đã là nửa đêm:
        // `DueDate.Date < today` ⟺ `DueDate < today`.
        return TasksOf(projectId)
            .CountAsync(t => t.DueDate != null
                          && t.DueDate < today
                          && t.Category != StatusCategory.Done, ct);
    }

    /// <summary>
    /// Đếm task theo CỘT (ADR-052).
    ///
    /// <para>
    /// 🔑 Bắt đầu từ bảng <c>BoardColumns</c> chứ không <c>GroupBy</c> trên Tasks — nhờ vậy
    /// <b>cột rỗng vẫn có mặt với số 0</b>. GroupBy chỉ nhìn thấy cột đang có task, và một
    /// biểu đồ thiếu cột khiến người đọc tưởng cột đó không tồn tại chứ không phải đang trống.
    /// Trước ADR-052 việc bù 0 do <c>StatisticsService.ZeroFill</c> làm dựa trên
    /// <c>Enum.GetValues</c>; nay danh mục nằm trong DB nên nguồn bù 0 cũng phải là DB.
    /// </para>
    /// <para>
    /// Global query filter tự loại task đã xóa mềm, kể cả bên trong <c>c.Tasks.Count()</c>.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<StatusTally>> TallyByStatusAsync(
        Guid projectId, CancellationToken ct = default)
        => await _context.BoardColumns
            .AsNoTracking()
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.Order).ThenBy(c => c.Id)
            .Select(c => new StatusTally(
                c.Id, c.Name, c.Color, c.Order, c.Category, c.Tasks.Count()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PriorityTally>> TallyByPriorityAsync(
        Guid projectId, CancellationToken ct = default)
        => await TasksOf(projectId)
            .GroupBy(t => t.Priority)
            .Select(g => new PriorityTally(g.Key, g.Count()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AssigneeTally>> TallyByAssigneeAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        // Đi từ TaskAssignments chứ không từ Tasks: một task có thể có NHIỀU người đảm nhận
        // (§5), nên "khối lượng theo người" phải đếm theo cặp (task, người) — group từ phía
        // Tasks sẽ chỉ đếm được mỗi task một lần.
        return await _context.TaskAssignments
            .AsNoTracking()
            .Where(a => a.Task.ProjectId == projectId)
            .GroupBy(a => new { a.EmployeeId, a.Employee.Name })
            // 🔴 Sắp xếp theo BIỂU THỨC TỔNG HỢP, trước khi projected sang `AssigneeTally`.
            //
            // Trước 2026-08-04 dòng này là `.OrderByDescending(x => x.Total)` đặt SAU
            // `.Select(...)` — EF không dịch được thứ tự trên property của một record vừa
            // dựng trong projection, nên nó ném `InvalidOperationException` và
            // `GET /projects/{id}/statistics` trả **500 ở MỌI lần gọi**, kể cả project rỗng.
            //
            // Endpoint đó được ghi ✅ "Xong 2026-08-03" và chưa từng có test nào chạm tới,
            // nên nó hỏng hoàn toàn suốt từ ngày viết ra mà không ai biết. Đây là lần thứ
            // năm dự án gặp đúng hình dạng lỗi này: *thứ cần kiểm chứng chưa có ai gọi tới.*
            // `StatisticsTests` sinh ra chính vì vậy — nó bắt được lỗi ngay ở lần chạy đầu.
            .OrderByDescending(g => g.Count())
            .Select(g => new AssigneeTally(
                g.Key.EmployeeId,
                g.Key.Name,
                g.Count(),
                g.Count(a => a.Task.Category == StatusCategory.Done),
                g.Count(a => a.Task.DueDate != null
                          && a.Task.DueDate < today
                          && a.Task.Category != StatusCategory.Done)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SprintTally>> TallyBySprintAsync(
        Guid projectId, CancellationToken ct = default)
        => await _context.Sprints
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.StartDate)
            .Select(s => new SprintTally(
                s.Id, s.Name, s.StartDate, s.EndDate,
                s.Tasks.Count,
                s.Tasks.Count(t => t.Category == StatusCategory.Done)))
            .ToListAsync(ct);
}
