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
        return TasksOf(projectId)
            .CountAsync(t => t.DueDate != null
                          && t.DueDate.Value.Date < today
                          && t.Status != Status.Done, ct);
    }

    public async Task<IReadOnlyList<StatusTally>> TallyByStatusAsync(
        Guid projectId, CancellationToken ct = default)
        => await TasksOf(projectId)
            .GroupBy(t => t.Status)
            .Select(g => new StatusTally(g.Key, g.Count()))
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
            .Select(g => new AssigneeTally(
                g.Key.EmployeeId,
                g.Key.Name,
                g.Count(),
                g.Count(a => a.Task.Status == Status.Done),
                g.Count(a => a.Task.DueDate != null
                          && a.Task.DueDate.Value.Date < today
                          && a.Task.Status != Status.Done)))
            .OrderByDescending(x => x.Total)
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
                s.Tasks.Count(t => t.Status == Status.Done)))
            .ToListAsync(ct);
}
