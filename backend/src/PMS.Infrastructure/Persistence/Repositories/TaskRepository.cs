using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Filtering;
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

    public async Task<PagedResult<TaskItem>> QueryAsync(
        Guid projectId, TaskQuerySpec spec, PagedRequest request, CancellationToken ct = default)
    {
        // Cùng chiến thuật HAI BƯỚC của GetPagedByProjectAsync, và ở đây còn cần thiết hơn:
        // bộ lọc trên trường tuỳ biến sinh subquery trên FieldValues, mà JOIN thêm ba
        // collection Include vào cùng câu đó thì số dòng nhân lên rất nhanh.
        var query = DbSet
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.ParentTaskId == null);

        foreach (var filter in spec.Filters)
            query = Apply(query, filter);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim();
            query = query.Where(t => t.Name.Contains(keyword));
        }

        var totalCount = await query.CountAsync(ct);

        query = Sort(query, spec.SortBy, spec.SortDescending);

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
            // FieldValues + lựa chọn đang chọn: màn danh sách hiển thị được CỘT là trường
            // tuỳ biến, nên thiếu Include ở đây thì mọi ô đó trống một cách im lặng — đúng
            // lớp lỗi SubtaskProgress-luôn-0 (ADR-034) và IsWatching-luôn-false (ADR-036).
            .Include(t => t.FieldValues).ThenInclude(v => v.SelectedOptions)
            .AsSplitQuery()
            .Where(t => pageIds.Contains(t.Id))
            .ToListAsync(ct);

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

    /// <summary>
    /// Sắp xếp theo một <see cref="TaskField"/> (danh mục ĐÓNG). Tie-break bằng <c>Id</c> ở
    /// MỌI nhánh — không trường nào duy nhất, nên thiếu nó thì hai lần gọi cùng một trang có
    /// thể trả thứ tự khác nhau và task nhảy trang khi người dùng bấm qua lại.
    /// </summary>
    private static IQueryable<TaskItem> Sort(
        IQueryable<TaskItem> query, TaskField? sortBy, bool descending)
    {
        // Sắp theo VỊ TRÍ cột chứ không theo tên (xem GetPagedByProjectAsync): "trạng thái
        // tăng dần" nghĩa là đi từ đầu quy trình tới cuối.
        var keyed = sortBy switch
        {
            TaskField.Name         => Order(query, t => t.Name, descending),
            TaskField.BoardColumn  => Order(query, t => t.BoardColumn.Order, descending),
            TaskField.Category     => Order(query, t => t.Category, descending),
            TaskField.Priority     => Order(query, t => t.Priority, descending),
            TaskField.WorkItemType => Order(query, t => t.WorkItemType.Order, descending),
            TaskField.Sprint       => Order(query, t => t.SprintId, descending),
            TaskField.Reporter     => Order(query, t => t.ReporterId, descending),
            TaskField.DueDate      => Order(query, t => t.DueDate, descending),
            TaskField.StoryPoints  => Order(query, t => t.StoryPoints, descending),
            TaskField.CreatedAt    => Order(query, t => t.CreatedAt, descending),
            // Assignee là quan hệ N–N: "sắp theo người đảm nhận" không có một khoá duy nhất
            // (một task có nhiều người), nên nó rơi về mặc định thay vì bịa ra một thứ tự.
            _                      => Order(query, t => t.DueDate, descending)
        };

        return keyed.ThenBy(t => t.Id);
    }

    private static IOrderedQueryable<TaskItem> Order<TKey>(
        IQueryable<TaskItem> query, System.Linq.Expressions.Expression<Func<TaskItem, TKey>> key,
        bool descending)
        => descending ? query.OrderByDescending(key) : query.OrderBy(key);

    /// <summary>
    /// Dịch MỘT điều kiện đã phân giải thành một <c>Where</c>.
    ///
    /// <para>
    /// Nhiều điều kiện nối bằng AND — mỗi lượt gọi chồng thêm một <c>Where</c>, đúng ngữ
    /// nghĩa đã ghi ở <see cref="SavedViewFilter"/>.
    /// </para>
    /// </summary>
    private static IQueryable<TaskItem> Apply(IQueryable<TaskItem> query, ResolvedFilter f)
        => f.IsCustomField ? ApplyCustom(query, f) : ApplyBuiltIn(query, f);

    private static IQueryable<TaskItem> ApplyBuiltIn(IQueryable<TaskItem> query, ResolvedFilter f)
    {
        var op = f.Operator;
        var negate = op is FilterOperator.NotEquals or FilterOperator.IsEmpty;

        return f.Field switch
        {
            TaskField.Name => op switch
            {
                FilterOperator.Contains   => query.Where(t => t.Name.Contains(f.Text!)),
                FilterOperator.Equals     => query.Where(t => t.Name == f.Text),
                FilterOperator.NotEquals  => query.Where(t => t.Name != f.Text),
                FilterOperator.IsEmpty    => query.Where(t => t.Name == ""),
                _                         => query.Where(t => t.Name != "")
            },

            TaskField.BoardColumn => negate
                ? query.Where(t => t.BoardColumnId != f.Reference)
                : query.Where(t => t.BoardColumnId == f.Reference),

            TaskField.WorkItemType => negate
                ? query.Where(t => t.WorkItemTypeId != f.Reference)
                : query.Where(t => t.WorkItemTypeId == f.Reference),

            TaskField.Reporter => negate
                ? query.Where(t => t.ReporterId != f.Reference)
                : query.Where(t => t.ReporterId == f.Reference),

            // Sprint rỗng = task đang ở Backlog. Đây là một trong hai chỗ IsEmpty mang nghĩa
            // nghiệp vụ thật (chỗ kia là Assignee = chưa ai nhận).
            TaskField.Sprint => op switch
            {
                FilterOperator.IsEmpty    => query.Where(t => t.SprintId == null),
                FilterOperator.IsNotEmpty => query.Where(t => t.SprintId != null),
                FilterOperator.NotEquals  => query.Where(t => t.SprintId != f.Reference),
                _                         => query.Where(t => t.SprintId == f.Reference)
            },

            TaskField.Assignee => op switch
            {
                FilterOperator.IsEmpty    => query.Where(t => !t.Assignments.Any()),
                FilterOperator.IsNotEmpty => query.Where(t => t.Assignments.Any()),
                // "khác X" = KHÔNG có X trong danh sách người đảm nhận. Đọc theo nghĩa
                // "có một người khác X" sẽ khiến một task giao cho cả X lẫn Y lọt lưới —
                // và đó không phải thứ người dùng muốn khi họ gõ "không phải của tôi".
                FilterOperator.NotEquals  => query.Where(t => !t.Assignments.Any(a => a.EmployeeId == f.Reference)),
                _                         => query.Where(t => t.Assignments.Any(a => a.EmployeeId == f.Reference))
            },

            TaskField.Category => negate
                ? query.Where(t => t.Category != (StatusCategory)f.EnumValue!.Value)
                : query.Where(t => t.Category == (StatusCategory)f.EnumValue!.Value),

            TaskField.Priority => negate
                ? query.Where(t => t.Priority != (Priority)f.EnumValue!.Value)
                : query.Where(t => t.Priority == (Priority)f.EnumValue!.Value),

            TaskField.DueDate => op switch
            {
                FilterOperator.IsEmpty            => query.Where(t => t.DueDate == null),
                FilterOperator.IsNotEmpty         => query.Where(t => t.DueDate != null),
                FilterOperator.Equals             => query.Where(t => t.DueDate == f.Date),
                FilterOperator.NotEquals          => query.Where(t => t.DueDate != f.Date),
                FilterOperator.GreaterThan        => query.Where(t => t.DueDate >  f.Date),
                FilterOperator.GreaterThanOrEqual => query.Where(t => t.DueDate >= f.Date),
                FilterOperator.LessThan           => query.Where(t => t.DueDate <  f.Date),
                _                                 => query.Where(t => t.DueDate <= f.Date)
            },

            // ⚠️ StoryPoints là int KHÔNG nullable, mặc định 0. Nên "rỗng" ở đây nghĩa là
            // CHƯA ƯỚC LƯỢNG (= 0), không phải NULL. Ghi rõ vì đây là chỗ duy nhất trong
            // catalog mà IsEmpty không đọc theo nghĩa đen.
            TaskField.StoryPoints => op switch
            {
                FilterOperator.IsEmpty            => query.Where(t => t.StoryPoints == 0),
                FilterOperator.IsNotEmpty         => query.Where(t => t.StoryPoints != 0),
                FilterOperator.Equals             => query.Where(t => t.StoryPoints == f.Number),
                FilterOperator.NotEquals          => query.Where(t => t.StoryPoints != f.Number),
                FilterOperator.GreaterThan        => query.Where(t => t.StoryPoints >  f.Number),
                FilterOperator.GreaterThanOrEqual => query.Where(t => t.StoryPoints >= f.Number),
                FilterOperator.LessThan           => query.Where(t => t.StoryPoints <  f.Number),
                _                                 => query.Where(t => t.StoryPoints <= f.Number)
            },

            // CreatedAt luôn có giá trị -> IsEmpty không bao giờ đúng. Trả về tập rỗng thay
            // vì bỏ qua điều kiện: bỏ qua sẽ khiến view trả về MỌI task và người dùng tưởng
            // bộ lọc của họ đang chạy.
            TaskField.CreatedAt => op switch
            {
                FilterOperator.IsEmpty            => query.Where(_ => false),
                FilterOperator.IsNotEmpty         => query,
                FilterOperator.Equals             => query.Where(t => t.CreatedAt == f.Date),
                FilterOperator.NotEquals          => query.Where(t => t.CreatedAt != f.Date),
                FilterOperator.GreaterThan        => query.Where(t => t.CreatedAt >  f.Date),
                FilterOperator.GreaterThanOrEqual => query.Where(t => t.CreatedAt >= f.Date),
                FilterOperator.LessThan           => query.Where(t => t.CreatedAt <  f.Date),
                _                                 => query.Where(t => t.CreatedAt <= f.Date)
            },

            _ => query
        };
    }

    /// <summary>
    /// Điều kiện trên trường TUỲ BIẾN (ADR-059) — một subquery trên <c>FieldValues</c>.
    ///
    /// <para>
    /// 🔴 So với <b>cột có kiểu</b>: <c>ValueNumber</c> cho số, <c>ValueDate</c> cho ngày.
    /// Đây đúng là thứ ADR-059 mua về khi từ chối một cột JSON — với JSON thì <c>"9" &gt;
    /// "10"</c> và không index nào dùng được.
    /// </para>
    /// <para>
    /// 📌 <b>"Rỗng" = KHÔNG CÓ HÀNG</b>, không phải hàng toàn null: ADR-059 xoá hẳn hàng khi
    /// người dùng xoá trắng giá trị, chính để mọi phép đếm "bao nhiêu task đã điền trường
    /// này" trả lời đúng. Bộ lọc ở đây thừa hưởng nguyên tính chất đó.
    /// </para>
    /// <para>
    /// 📌 <b>"khác X" bao gồm cả task CHƯA ĐIỀN.</b> Đọc theo nghĩa "có giá trị và giá trị
    /// đó khác X" sẽ loại bỏ task chưa điền — trong khi người dùng gõ "Mức rủi ro khác Cao"
    /// thì họ mong thấy cả những việc chưa ai đánh giá rủi ro.
    /// </para>
    /// </summary>
    private static IQueryable<TaskItem> ApplyCustom(IQueryable<TaskItem> query, ResolvedFilter f)
    {
        var fieldId = f.FieldDefinitionId!.Value;

        if (f.Operator is FilterOperator.IsEmpty)
            return query.Where(t => !t.FieldValues.Any(v => v.FieldDefinitionId == fieldId));

        if (f.Operator is FilterOperator.IsNotEmpty)
            return query.Where(t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId));

        System.Linq.Expressions.Expression<Func<TaskItem, bool>> match = f.Kind switch
        {
            FilterValueKind.Text when f.Operator is FilterOperator.Contains =>
                t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId
                                         && v.ValueText != null && v.ValueText.Contains(f.Text!)),

            FilterValueKind.Text =>
                t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId && v.ValueText == f.Text),

            FilterValueKind.Boolean =>
                t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId && v.ValueBoolean == f.Boolean),

            // Select: giá trị là Id của một FieldOption đang được chọn.
            FilterValueKind.Reference =>
                t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId
                                         && v.SelectedOptions.Any(o => o.Id == f.Reference)),

            FilterValueKind.Number => f.Operator switch
            {
                FilterOperator.GreaterThan        => t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId && v.ValueNumber >  f.Number),
                FilterOperator.GreaterThanOrEqual => t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId && v.ValueNumber >= f.Number),
                FilterOperator.LessThan           => t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId && v.ValueNumber <  f.Number),
                FilterOperator.LessThanOrEqual    => t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId && v.ValueNumber <= f.Number),
                _                                 => t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId && v.ValueNumber == f.Number)
            },

            FilterValueKind.Date => f.Operator switch
            {
                FilterOperator.GreaterThan        => t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId && v.ValueDate >  f.Date),
                FilterOperator.GreaterThanOrEqual => t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId && v.ValueDate >= f.Date),
                FilterOperator.LessThan           => t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId && v.ValueDate <  f.Date),
                FilterOperator.LessThanOrEqual    => t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId && v.ValueDate <= f.Date),
                _                                 => t => t.FieldValues.Any(v => v.FieldDefinitionId == fieldId && v.ValueDate == f.Date)
            },

            _ => _ => false
        };

        // NotEquals đảo cả vị từ "có giá trị bằng X" -> "không có giá trị bằng X", nên task
        // chưa điền cũng lọt vào. Xem chú thích trên.
        return f.Operator is FilterOperator.NotEquals
            ? query.Where(Negate(match))
            : query.Where(match);
    }

    private static System.Linq.Expressions.Expression<Func<TaskItem, bool>> Negate(
        System.Linq.Expressions.Expression<Func<TaskItem, bool>> expression)
        => System.Linq.Expressions.Expression.Lambda<Func<TaskItem, bool>>(
            System.Linq.Expressions.Expression.Not(expression.Body), expression.Parameters);

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
    public async Task<IReadOnlyList<TaskItem>> GetMyOpenAssignedTasksAsync(
        Guid employeeId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            .Include(t => t.Assignments).ThenInclude(a => a.Employee)
            .Include(t => t.Subtasks)
            .Include(t => t.Labels)
            .Include(t => t.Project)
            .AsSplitQuery()
            .Where(t => t.Assignments.Any(a => a.EmployeeId == employeeId)
                     && t.Category != StatusCategory.Done)
            // Task chưa có hạn để cuối: màn này ưu tiên lịch làm việc đã có mốc thời gian,
            // nhưng không giấu việc chỉ vì PM chưa đặt hạn.
            .OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenBy(t => t.Priority).ThenBy(t => t.Id)
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
