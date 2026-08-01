using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Infrastructure.Persistence.Repositories;

public class TaskRepository : Repository<TaskItem>, ITaskRepository
{
    public TaskRepository(PmsDbContext context) : base(context) { }

    public async Task<TaskItem?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(t => t.Reporter)
            .Include(t => t.Assignments).ThenInclude(a => a.Employee)
            // Subtask cũng map qua ToSummary nên cũng cần Assignments, nếu không
            // TaskDetailResponse.Subtasks[].Assignees rỗng một cách im lặng.
            .Include(t => t.Subtasks).ThenInclude(s => s.Assignments).ThenInclude(a => a.Employee)
            .Include(t => t.Labels)
            .Include(t => t.Comments).ThenInclude(c => c.Author)
            .Include(t => t.OutgoingLinks).ThenInclude(l => l.TargetTask)
            .Include(t => t.IncomingLinks).ThenInclude(l => l.SourceTask)
            .AsSplitQuery()   // tách thành nhiều câu SQL, tránh "cartesian explosion" khi Include nhiều collection
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<TaskItem?> GetWithSubtasksAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(t => t.Subtasks)
            .Include(t => t.Assignments).ThenInclude(a => a.Employee)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<TaskItem?> GetWithAssignmentsAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(t => t.Assignments).ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<TaskItem?> GetForStatusChangeAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            // ThenInclude(Employee) BẮT BUỘC: endpoint đổi status trả TaskSummaryResponse,
            // mà ToSummary đọc Assignment.Employee.Name. Thiếu nó thì Employee là null và
            // mapper ném NullReferenceException → 500, không phải lỗi map im lặng.
            .Include(t => t.Assignments).ThenInclude(a => a.Employee)
            .Include(t => t.Watchers)
            .Include(t => t.Subtasks)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<TaskItem?> GetWithNotificationTargetsAsync(
        Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(t => t.Assignments)
            .Include(t => t.Watchers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<PagedResult<TaskItem>> GetPagedByProjectAsync(
        Guid projectId, PagedRequest request, CancellationToken ct = default)
    {
        var query = DbSet
            .AsNoTracking()
            // Cùng lý do với Board/Backlog ở dưới — kết quả cũng map qua ToSummary.
            // KHÔNG dùng AsSplitQuery ở đây: query này có Skip/Take mà OrderBy theo
            // DueDate/Name không duy nhất, split query với phân trang không xác định
            // được thứ tự nên có thể trả về dữ liệu lệch giữa các câu SQL. Một câu duy
            // nhất thì số dòng nhân lên nhưng bị chặn bởi PageSize (tối đa 100).
            .Include(t => t.Assignments).ThenInclude(a => a.Employee)
            .Include(t => t.Subtasks)
            .Where(t => t.ProjectId == projectId && t.ParentTaskId == null);  // chỉ task gốc

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim();
            query = query.Where(t => t.Name.Contains(keyword));
        }

        var totalCount = await query.CountAsync(ct);

        query = (request.SortBy?.ToLowerInvariant(), request.IsDescending) switch
        {
            ("name", false)     => query.OrderBy(t => t.Name),
            ("name", true)      => query.OrderByDescending(t => t.Name),
            ("priority", false) => query.OrderBy(t => t.Priority),
            ("priority", true)  => query.OrderByDescending(t => t.Priority),
            ("status", false)   => query.OrderBy(t => t.Status),
            ("status", true)    => query.OrderByDescending(t => t.Status),
            (_, true)           => query.OrderByDescending(t => t.DueDate),
            _                   => query.OrderBy(t => t.DueDate)
        };

        var items = await query.Skip(request.Skip).Take(request.PageSize).ToListAsync(ct);
        return new PagedResult<TaskItem>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    // ⚠️ Ba query dưới đây nuôi Board và Backlog, tức là nguồn của TaskSummaryResponse.
    // Cả hai Include đều BẮT BUỘC, không phải tối ưu:
    //   • Assignments -> TaskSummaryResponse.Assignees (avatar trên thẻ, và là dữ liệu
    //     duy nhất cho client biết "tôi có phải assignee không" để gác quyền đổi status
    //     theo ADR-017 mà không phải gọi N+1 lần /tasks/{id}/assignees).
    //   • Subtasks -> TaskItem.SubtaskProgress đọc Subtasks.Count. Thiếu Include thì
    //     collection rỗng và progress LUÔN trả 0 — sai một cách im lặng, không lỗi nào.
    // AsSplitQuery vì có hai collection: JOIN chung sẽ nhân dòng (cartesian explosion),
    // cùng lý do đã dùng ở GetWithDetailsAsync.

    public async Task<IReadOnlyList<TaskItem>> GetBacklogAsync(
        Guid projectId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            .Include(t => t.Assignments).ThenInclude(a => a.Employee)
            .Include(t => t.Subtasks)
            .AsSplitQuery()
            .Where(t => t.ProjectId == projectId && t.SprintId == null && t.ParentTaskId == null)
            .OrderBy(t => t.Priority)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> GetRootTasksByProjectAsync(
        Guid projectId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            .Include(t => t.Assignments).ThenInclude(a => a.Employee)
            .Include(t => t.Subtasks)
            .AsSplitQuery()
            .Where(t => t.ProjectId == projectId && t.ParentTaskId == null)
            .OrderBy(t => t.Priority)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> GetBySprintAsync(
        Guid sprintId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            .Include(t => t.Assignments).ThenInclude(a => a.Employee)
            .Include(t => t.Subtasks)
            .AsSplitQuery()
            .Where(t => t.SprintId == sprintId)
            .OrderBy(t => t.Priority)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> GetUnfinishedBlockersAsync(
        Guid taskId, CancellationToken ct = default)
    {
        // Task X bị chặn khi: có link (A --Blocks--> X) hoặc (X --IsBlockedBy--> A).
        var blockerIds = Context.TaskLinks
            .Where(l => (l.TargetTaskId == taskId && l.LinkType == LinkType.Blocks)
                     || (l.SourceTaskId == taskId && l.LinkType == LinkType.IsBlockedBy))
            .Select(l => l.LinkType == LinkType.Blocks ? l.SourceTaskId : l.TargetTaskId);

        return await DbSet
            .AsNoTracking()
            .Where(t => blockerIds.Contains(t.Id) && t.Status != Status.Done)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskItem>> GetOverdueAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        return await DbSet
            .AsNoTracking()
            .Where(t => t.DueDate != null
                     && t.DueDate.Value.Date < today
                     && t.Status != Status.Done)
            .ToListAsync(ct);
    }

    public async Task<int> CountActiveAssignedAsync(Guid projectId, Guid employeeId, CancellationToken ct = default)
        => await DbSet.CountAsync(
            t => t.ProjectId == projectId
            && t.Status != Status.Done
            && t.Assignments.Any(a => a.EmployeeId == employeeId), ct
        );
}
