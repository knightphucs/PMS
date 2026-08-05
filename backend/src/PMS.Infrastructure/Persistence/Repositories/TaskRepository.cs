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
            // Project BẮT BUỘC: ToDetail cần Project.Key để ghép mã PMS-12 (ADR-034).
            // Reference include nên không nhân dòng.
            .Include(t => t.Project)
            .Include(t => t.Assignments).ThenInclude(a => a.Employee)
            // Watchers BẮT BUỘC: TaskDetailResponse.IsWatching đọc collection này. Thiếu
            // Include thì nó rỗng và IsWatching LUÔN false — sai im lặng, đúng lớp lỗi
            // SubtaskProgress-luôn-0 đã ghi ở §1 (ADR-036).
            .Include(t => t.Watchers)
            // Subtask cũng map qua ToSummary nên cũng cần Assignments + Labels, nếu không
            // TaskDetailResponse.Subtasks[].Assignees/Labels rỗng một cách im lặng.
            .Include(t => t.Subtasks).ThenInclude(s => s.Assignments).ThenInclude(a => a.Employee)
            .Include(t => t.Subtasks).ThenInclude(s => s.Labels)
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

    // KHÔNG AsNoTracking: caller sửa collection Labels rồi SaveChanges, nên EF phải theo dõi.
    public async Task<TaskItem?> GetWithLabelsAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(t => t.Labels)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<TaskItem?> GetForStatusChangeAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            // ThenInclude(Employee) BẮT BUỘC: endpoint đổi status trả TaskSummaryResponse,
            // mà ToSummary đọc Assignment.Employee.Name. Thiếu nó thì Employee là null và
            // mapper ném NullReferenceException → 500, không phải lỗi map im lặng.
            .Include(t => t.Assignments).ThenInclude(a => a.Employee)
            .Include(t => t.Watchers)
            .Include(t => t.Subtasks)
            // Labels: ToSummary trả chip nhãn cho thẻ Kanban. Thiếu Include thì thẻ vừa
            // kéo–thả xong bị MẤT hết nhãn cho tới lần refetch sau — sai im lặng.
            .Include(t => t.Labels)
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
        // 🔴 HAI BƯỚC, không phải một. Trước đây đây là một query duy nhất với hai
        // collection Include và CỐ Ý không AsSplitQuery (split + Skip/Take trên OrderBy
        // không duy nhất thì thứ tự giữa các câu SQL không xác định). Nhưng thêm Labels
        // là collection THỨ BA, và JOIN ba collection trong một câu thì số dòng nhân lên
        // theo assignees × subtasks × labels.
        //
        // Cách thoát khỏi thế lưỡng nan: phân trang trên query KHÔNG có Include (chỉ lấy
        // Id — thứ tự hoàn toàn xác định), rồi nạp lại đúng các Id đó với đủ Include +
        // AsSplitQuery. Split query lúc này an toàn vì không còn Skip/Take.
        // Đồng thời khử luôn phép nhân dòng vốn đã có sẵn với hai collection.
        var query = DbSet
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.ParentTaskId == null);  // chỉ task gốc

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim();
            query = query.Where(t => t.Name.Contains(keyword));
        }

        var totalCount = await query.CountAsync(ct);

        // Tie-break bằng Id: DueDate/Name/Priority/Status đều không duy nhất, nên thiếu nó
        // thì hai lần gọi cùng một trang có thể trả về thứ tự khác nhau và làm task nhảy
        // trang khi người dùng bấm qua lại.
        query = (request.SortBy?.ToLowerInvariant(), request.IsDescending) switch
        {
            ("name", false)     => query.OrderBy(t => t.Name).ThenBy(t => t.Id),
            ("name", true)      => query.OrderByDescending(t => t.Name).ThenBy(t => t.Id),
            ("priority", false) => query.OrderBy(t => t.Priority).ThenBy(t => t.Id),
            ("priority", true)  => query.OrderByDescending(t => t.Priority).ThenBy(t => t.Id),
            // Sắp theo VỊ TRÍ cột trái->phải, không theo tên: "status tăng dần" nghĩa là
            // đi từ đầu quy trình tới cuối, còn sắp theo tên thì "Đang làm" đứng trước
            // "Cần làm" chỉ vì chữ Đ trước chữ C.
            ("status", false)   => query.OrderBy(t => t.BoardColumn.Order).ThenBy(t => t.Id),
            ("status", true)    => query.OrderByDescending(t => t.BoardColumn.Order).ThenBy(t => t.Id),
            (_, true)           => query.OrderByDescending(t => t.DueDate).ThenBy(t => t.Id),
            _                   => query.OrderBy(t => t.DueDate).ThenBy(t => t.Id)
        };

        var pageIds = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(t => t.Id)
            .ToListAsync(ct);

        var loaded = await DbSet
            .AsNoTracking()
            .Include(t => t.Assignments).ThenInclude(a => a.Employee)
            .Include(t => t.Subtasks)
            .Include(t => t.Labels)
            .AsSplitQuery()
            .Where(t => pageIds.Contains(t.Id))
            .ToListAsync(ct);

        // Bước hai mất thứ tự (WHERE IN không giữ thứ tự) -> sắp lại theo đúng pageIds.
        var byId = loaded.ToDictionary(t => t.Id);
        var items = pageIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();

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
            .Include(t => t.Labels)
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
            .Include(t => t.Labels)
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
            .Include(t => t.Labels)
            .AsSplitQuery()
            .Where(t => t.SprintId == sprintId)
            .OrderBy(t => t.Priority)
            .ToListAsync(ct);

    /// <summary>
    /// Việc của MỘT người, XUYÊN mọi dự án họ tham gia (ADR-053).
    ///
    /// <para>
    /// Lọc: được gán cho người đó · chưa thuộc nhóm <c>Done</c> · có hạn và hạn ≤ hôm nay.
    /// "≤" chứ không "=" là cố ý — việc trễ hạn phải nổi lên cùng việc hôm nay, giấu nó đi
    /// là đúng cách để nó bị quên tiếp.
    /// </para>
    /// <para>
    /// ⚠️ <c>Include(Project)</c> vì kết quả cần tên dự án để gom nhóm; đây là endpoint duy
    /// nhất không có <c>projectId</c> trong URL nên client không tự biết được.
    /// </para>
    /// <para>
    /// ⚠️ Không cần lọc "còn là thành viên project": <c>TaskAssignment</c> bị gỡ khi người
    /// đó rời dự án, nên phép nối theo assignment đã bao hàm điều kiện ấy.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<TaskItem>> GetMyDueTasksAsync(
        Guid employeeId, DateTime todayUtc, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            .Include(t => t.Assignments).ThenInclude(a => a.Employee)
            .Include(t => t.Subtasks)
            .Include(t => t.Labels)
            .Include(t => t.Project)
            .AsSplitQuery()
            .Where(t => t.Assignments.Any(a => a.EmployeeId == employeeId)
                     && t.Category != StatusCategory.Done
                     && t.DueDate != null
                     // So THẲNG với mốc nửa đêm, không dùng `.Value.Date`: mọi cột DateTime
                     // đi qua ValueConverter đóng dấu Kind=Utc (ADR-046b), và EF KHÔNG dịch
                     // được `.Date` trên cột đã chuyển đổi — nó ném lúc chạy thành HTTP 500.
                     && t.DueDate < todayUtc.AddDays(1))
            .OrderBy(t => t.DueDate).ThenBy(t => t.Priority).ThenBy(t => t.Id)
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
            .Where(t => blockerIds.Contains(t.Id) && t.Category != StatusCategory.Done)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskItem>> GetOverdueAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        // ⚠️ So sánh THẲNG với mốc nửa đêm, KHÔNG dùng `.Value.Date`. Từ 2026-08-04 mọi cột
        // DateTime có ValueConverter (đóng dấu Kind=Utc lúc đọc), và EF **không dịch được
        // member access `.Date` trên cột đã chuyển đổi** — nó ném ngay lúc chạy, thành HTTP
        // 500. Hai vế tương đương về mặt toán học vì `today`/`horizon` đã là nửa đêm:
        // `DueDate.Date < today` ⟺ `DueDate < today`.
        return await DbSet
            .AsNoTracking()
            .Where(t => t.DueDate != null
                     && t.DueDate < today
                     && t.Category != StatusCategory.Done)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskItem>> GetDueSoonOrOverdueWithTargetsAsync(
        int horizonDays, CancellationToken ct = default)
    {
        // Cộng thêm 1 ngày rồi so `<`: tương đương `DueDate.Date <= horizon` nhưng dịch
        // được sang SQL sau khi cột DueDate có ValueConverter (xem GetOverdueAsync).
        var horizonExclusive = DateTime.UtcNow.Date.AddDays(horizonDays + 1);

        return await DbSet
            .AsNoTracking()
            // BẮT BUỘC cho InterestedEmployeeIds() — xem chú thích ở ITaskRepository.
            .Include(t => t.Assignments)
            .Include(t => t.Watchers)
            .AsSplitQuery()
            .Where(t => t.DueDate != null
                     && t.DueDate < horizonExclusive
                     && t.Category != StatusCategory.Done)
            .ToListAsync(ct);
    }

    public async Task<int> CountActiveAssignedAsync(Guid projectId, Guid employeeId, CancellationToken ct = default)
        => await DbSet.CountAsync(
            t => t.ProjectId == projectId
            && t.Category != StatusCategory.Done
            && t.Assignments.Any(a => a.EmployeeId == employeeId), ct
        );
}
