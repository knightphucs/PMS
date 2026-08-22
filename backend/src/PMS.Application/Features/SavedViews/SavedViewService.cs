using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Filtering;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Application.Features.CustomFields;
using PMS.Application.Features.Tasks;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.SavedViews;

/// <summary>
/// View lưu được (ADR-061) — mảnh cuối của nền tảng mở rộng, và tiền đề của "hàng đợi".
///
/// <para>
/// 🔑 <b>Hai mức quyền, và ranh giới giữa chúng là điểm thiết kế chính của lớp này.</b>
/// View <b>RIÊNG</b>: ai cũng tạo được, kể cả <c>Viewer</c> — nó chỉ đổi cách chính họ nhìn
/// danh sách, không ai khác thấy (cùng lý lẽ <c>Watch</c> ở ADR-036). View <b>CHIA SẺ</b>:
/// cần <see cref="ProjectAction.ManageSavedViews"/>, vì nó là cấu hình cả đội nhìn thấy —
/// cùng mức với cột board (ADR-052), trường (ADR-059) và loại việc (ADR-060).
/// </para>
/// <para>
/// Luật "chủ sở hữu tự quản view của mình" cần dữ liệu per-row nên nằm ở đây chứ không ở
/// <c>ProjectPermissions</c> — đúng "ranh giới còn lại" của ADR-019.
/// </para>
/// </summary>
public class SavedViewService : ISavedViewService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly TaskMapper _taskMapper;
    private readonly ILogger<SavedViewService> _logger;

    public SavedViewService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        IActivityLogger activityLog, TaskMapper taskMapper, ILogger<SavedViewService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _taskMapper = taskMapper;
        _logger = logger;
    }

    // ---------- khai báo ----------

    public async Task<IReadOnlyList<SavedViewResponse>> ListAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var role = await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);
        var me = _currentUser.RequireEmployeeId();

        var canManageShared = ProjectPermissions.IsAllowed(ProjectAction.ManageSavedViews, role);
        var views = await _uow.SavedViews.ListVisibleAsync(projectId, me, ct);

        return views.Select(v => ToResponse(v, me, canManageShared)).ToList();
    }

    public async Task<SavedViewResponse> CreateAsync(
        Guid projectId, CreateSavedViewRequest request, CancellationToken ct = default)
    {
        // View riêng chỉ cần View; chia sẻ mới cần ManageSavedViews. Kiểm mức thấp trước để
        // người ngoài project nhận 404 (ADR-006) chứ không phải 403.
        var role = await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);
        var me = _currentUser.RequireEmployeeId();

        if (request.IsShared)
            RequireCanManageShared(role);

        var name = request.Name.Trim();
        var mine = (await _uow.SavedViews.ListVisibleAsync(projectId, me, ct))
            .Where(v => v.OwnerId == me)
            .ToList();

        // Kiểm ở đây thay vì để unique index ném DbUpdateException -> 500. Cùng khuôn
        // CustomFieldService/BoardColumnService.
        if (mine.Any(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"Bạn đã có một view tên '{name}' trong dự án này.");

        var all = await _uow.SavedViews.ListVisibleAsync(projectId, me, ct);

        var view = new SavedView
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            OwnerId = me,
            IsShared = request.IsShared,
            SortBy = request.SortBy,
            SortDescending = request.SortDescending,
            GroupBy = request.GroupBy,
            Order = all.Count == 0 ? 0 : all.Max(v => v.Order) + 1,
        };

        await ApplyPartsAsync(view, projectId, request.Filters, request.Columns, ct);
        await _uow.SavedViews.AddAsync(view, ct);

        _activityLog.Log(nameof(Project), projectId, ActivityAction.Updated,
            $"Tạo view '{view.Name}'{(view.IsShared ? " (chia sẻ)" : "")}");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Tạo view {ViewId} '{Name}' cho project {ProjectId}",
            view.Id, view.Name, projectId);

        // 🔴 Owner CHƯA được nạp: entity vừa dựng trong bộ nhớ nên EF không nạp navigation
        // hộ, và ToResponse đọc `view.Owner.Name`. Đây đúng cái bẫy đã làm `CreateAsync` của
        // ADR-060 ném NRE ở MỌI lần tạo task (đặt khoá ngoại mà quên navigation).
        // Đọc tên một lần từ repository thay vì thêm `Name` vào ICurrentUserService — file
        // đó ghi rõ: đừng thêm member không có người đọc.
        var owner = await _uow.Employees.GetByIdAsync(me, ct);

        return ToResponse(view, me, ProjectPermissions.IsAllowed(ProjectAction.ManageSavedViews, role),
                          ownerNameOverride: owner?.Name ?? string.Empty);
    }

    public async Task<SavedViewResponse> UpdateAsync(
        Guid viewId, UpdateSavedViewRequest request, CancellationToken ct = default)
    {
        var (view, role, me) = await RequireWritableAsync(viewId, ct);

        // Chuyển một view riêng thành chia sẻ là một hành động ở mức "cấu hình cả đội", kể
        // cả khi người làm là chủ sở hữu.
        if (request.IsShared && !view.IsShared)
            RequireCanManageShared(role);

        var name = request.Name.Trim();
        var siblings = (await _uow.SavedViews.ListVisibleAsync(view.ProjectId, view.OwnerId, ct))
            .Where(v => v.OwnerId == view.OwnerId && v.Id != viewId)
            .ToList();

        if (siblings.Any(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"Đã có một view tên '{name}' trong dự án này.");

        view.Name = name;
        view.IsShared = request.IsShared;
        view.SortBy = request.SortBy;
        view.SortDescending = request.SortDescending;
        view.GroupBy = request.GroupBy;

        // Ghi đè TOÀN PHẦN: xoá sạch rồi dựng lại. EF sinh DELETE cho phần tử biến mất.
        view.Filters.Clear();
        view.Columns.Clear();
        await ApplyPartsAsync(view, view.ProjectId, request.Filters, request.Columns, ct);

        _activityLog.Log(nameof(Project), view.ProjectId, ActivityAction.Updated,
            $"Sửa view '{view.Name}'");

        await _uow.SaveChangesAsync(ct);

        return ToResponse(view, me, ProjectPermissions.IsAllowed(ProjectAction.ManageSavedViews, role));
    }

    public async Task DeleteAsync(Guid viewId, CancellationToken ct = default)
    {
        var (view, _, _) = await RequireWritableAsync(viewId, ct);

        // Filters và Columns biến mất theo nhờ FK Cascade (SavedViewConfigurations) — khác
        // hẳn FieldDefinition, nơi phải xoá giá trị tường minh vì FK ở đó buộc phải là
        // Restrict để tránh hai đường cascade (ADR-059).
        _uow.SavedViews.Remove(view);

        _activityLog.Log(nameof(Project), view.ProjectId, ActivityAction.Updated,
            $"Xoá view '{view.Name}'");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Xoá view {ViewId} '{Name}'", view.Id, view.Name);
    }

    public async Task ReorderAsync(
        Guid projectId, ReorderSavedViewsRequest request, CancellationToken ct = default)
    {
        var role = await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);
        var me = _currentUser.RequireEmployeeId();

        var visible = await _uow.SavedViews.ListVisibleAsync(projectId, me, ct);
        var ids = request.ViewIds;

        // Đòi danh sách ĐẦY ĐỦ các view người này THẤY — cùng khuôn PUT /columns/order và
        // /fields/order. Nhận một phần thì phải định nghĩa "phần còn lại đi đâu", và mọi câu
        // trả lời đều là một luật ngầm client phải đoán.
        if (ids.Count != visible.Count || ids.Distinct().Count() != ids.Count
            || ids.Any(id => visible.All(v => v.Id != id)))
            throw new BusinessRuleException(
                "Danh sách sắp xếp phải chứa ĐÚNG MỘT LẦN mỗi view bạn đang thấy trong dự án.");

        var canManageShared = ProjectPermissions.IsAllowed(ProjectAction.ManageSavedViews, role);

        for (var i = 0; i < ids.Count; i++)
        {
            var view = await _uow.SavedViews.GetByIdAsync(ids[i], ct);
            if (view is null) continue;

            // Đổi thứ tự một view CHIA SẺ là đổi thứ họ thấy trên thanh view của mọi người.
            // Bỏ qua thay vì ném: người dùng đang sắp xếp thanh view của chính mình, và một
            // 403 giữa chừng sẽ để lại thứ tự dở dang.
            if (view.IsShared && !canManageShared) continue;

            view.Order = i;
        }

        _activityLog.Log(nameof(Project), projectId, ActivityAction.Updated,
            $"Đổi thứ tự {ids.Count} view");

        await _uow.SaveChangesAsync(ct);
    }

    // ---------- chạy view ----------

    public async Task<PagedResult<TaskListItemResponse>> QueryTasksAsync(
        Guid projectId, TaskQueryRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);

        var project = await _uow.Projects.GetByIdAsync(projectId, ct)
            ?? throw new NotFoundException(nameof(Project), projectId);

        var definitions = await _uow.FieldDefinitions.ListByProjectAsync(projectId, ct);
        var spec = new TaskQuerySpec(
            ResolveFilters(request.Filters, definitions),
            request.SortBy,
            request.SortDescending);

        var paging = new PagedRequest
        {
            Page = request.Page,
            PageSize = request.PageSize,
            Search = request.Search,
        };

        var page = await _uow.Tasks.QueryAsync(projectId, spec, paging, ct);
        var byId = definitions.ToDictionary(d => d.Id);

        return page.Map(task => new TaskListItemResponse(
            _taskMapper.ToSummary(task, project.Key),
            task.FieldValues
                .Where(v => byId.ContainsKey(v.FieldDefinitionId))
                .Select(v => ToListValue(v, byId[v.FieldDefinitionId]))
                .OrderBy(v => byId[v.FieldDefinitionId].Order)
                .ToList()));
    }

    // ---------- private ----------

    private void RequireCanManageShared(RoleInProject role)
    {
        if (!ProjectPermissions.IsAllowed(ProjectAction.ManageSavedViews, role))
            throw new ForbiddenException(
                "Chỉ quản trị dự án mới tạo hoặc sửa được view dùng chung.");
    }

    /// <summary>
    /// Nạp view và kiểm quyền GHI.
    ///
    /// <para>
    /// 🔴 Hai luật khác nhau, không gộp được:
    /// <list type="bullet">
    /// <item>View <b>RIÊNG</b>: chỉ chủ sở hữu. Kể cả PM cũng không — nó không nằm trong tầm
    /// nhìn của ai khác, nên "quản lý" nó không có nghĩa gì.</item>
    /// <item>View <b>CHIA SẺ</b>: chủ sở hữu HOẶC người có <c>ManageSavedViews</c>. Chỉ chủ
    /// sở hữu sẽ khoá chết view chung của cả đội khi người đó rời dự án; mọi thành viên thì
    /// ai cũng ghi đè công của nhau (ADR-061).</item>
    /// </list>
    /// </para>
    /// </summary>
    private async Task<(SavedView View, RoleInProject Role, Guid Me)> RequireWritableAsync(
        Guid viewId, CancellationToken ct)
    {
        var view = await _uow.SavedViews.GetWithPartsAsync(viewId, ct)
            ?? throw new NotFoundException(nameof(SavedView), viewId);

        // Quyền kiểm theo PROJECT của view: người ngoài project nhận 404 từ AuthorizeAsync
        // (ADR-006), không phải 403 — 403 xác nhận id có thật.
        var role = await _authz.AuthorizeAsync(view.ProjectId, ProjectAction.View, ct);
        var me = _currentUser.RequireEmployeeId();

        if (view.OwnerId == me) return (view, role, me);

        // Không phải chủ sở hữu, mà view lại là RIÊNG của người khác -> 404 chứ không 403.
        // 403 sẽ xác nhận "view này có thật và thuộc về ai đó", tức tiết lộ đúng thứ mà
        // ListVisibleAsync đã cố ý không trả về.
        if (!view.IsShared) throw new NotFoundException(nameof(SavedView), viewId);

        RequireCanManageShared(role);
        return (view, role, me);
    }

    /// <summary>
    /// Dựng lại <c>Filters</c> + <c>Columns</c> từ request, có kiểm đầy đủ.
    /// <para>
    /// 🔴 Trường tuỳ biến phải thuộc ĐÚNG project này — nếu không thì một view lọc theo
    /// trường của project khác, dữ liệu không bao giờ khớp và người dùng không hiểu vì sao.
    /// Cùng chốt chặn "ranh giới chéo project" mà ADR-059 đặt ở <c>SetValuesAsync</c>.
    /// </para>
    /// </summary>
    private async Task ApplyPartsAsync(
        SavedView view, Guid projectId,
        IReadOnlyList<SavedViewFilterDto>? filters,
        IReadOnlyList<SavedViewColumnDto>? columns,
        CancellationToken ct)
    {
        var definitions = (await _uow.FieldDefinitions.ListByProjectAsync(projectId, ct))
            .ToDictionary(d => d.Id);

        foreach (var dto in filters ?? [])
        {
            var (label, type) = DescribeField(dto.Field, dto.FieldDefinitionId, definitions);

            // Phân giải NGAY tại đây — parse literal, kiểm toán tử hợp lệ với kiểu. Ném 400
            // kèm thông điệp đọc được. Để tới lúc CHẠY view mới báo lỗi là để người dùng ôm
            // một view hỏng mà không biết mình đã lưu sai cái gì.
            TaskFilterCatalog.Resolve(dto.Field, dto.FieldDefinitionId, type,
                                      dto.Operator, dto.Value, label);

            view.Filters.Add(new SavedViewFilter
            {
                Id = Guid.NewGuid(),
                SavedViewId = view.Id,
                Field = dto.Field,
                FieldDefinitionId = dto.FieldDefinitionId,
                Operator = dto.Operator,
                Value = dto.Value?.Trim(),
            });
        }

        var order = 0;
        foreach (var dto in columns ?? [])
        {
            DescribeField(dto.Field, dto.FieldDefinitionId, definitions);

            view.Columns.Add(new SavedViewColumn
            {
                Id = Guid.NewGuid(),
                SavedViewId = view.Id,
                Field = dto.Field,
                FieldDefinitionId = dto.FieldDefinitionId,
                Order = order++,
            });
        }
    }

    /// <summary>
    /// Kiểm "đúng một nguồn trường" + trường tuỳ biến thuộc project này, trả về tên hiển thị
    /// và kiểu (null nếu là trường dựng sẵn).
    /// </summary>
    private static (string Label, FieldType? Type) DescribeField(
        TaskField? field, Guid? fieldDefinitionId,
        IReadOnlyDictionary<Guid, FieldDefinition> definitions)
    {
        if (field.HasValue == fieldDefinitionId.HasValue)
            throw new BusinessRuleException(
                "Mỗi bộ lọc và mỗi cột phải nhắm vào ĐÚNG MỘT trong hai: một trường dựng sẵn " +
                "hoặc một trường tuỳ biến.");

        if (field.HasValue) return (field.Value.ToString(), null);

        if (!definitions.TryGetValue(fieldDefinitionId!.Value, out var definition))
            // 404 chứ không nuốt lặng: id này hoặc là rác, hoặc là trường của project khác.
            throw new NotFoundException(
                $"Trường tuỳ biến {fieldDefinitionId} không thuộc project này.");

        return (definition.Label, definition.Type);
    }

    private static IReadOnlyList<ResolvedFilter> ResolveFilters(
        IReadOnlyList<SavedViewFilterDto>? filters,
        IReadOnlyList<FieldDefinition> definitions)
    {
        if (filters is null || filters.Count == 0) return [];

        var byId = definitions.ToDictionary(d => d.Id);

        return filters.Select(f =>
        {
            var (label, type) = DescribeField(f.Field, f.FieldDefinitionId, byId);
            return TaskFilterCatalog.Resolve(
                f.Field, f.FieldDefinitionId, type, f.Operator, f.Value, label);
        }).ToList();
    }

    private static TaskListFieldValue ToListValue(FieldValue value, FieldDefinition definition)
        => new(definition.Id, definition.Label, definition.Type,
               value.ValueText, value.ValueNumber, value.ValueDate, value.ValueBoolean,
               definition.Options
                   .Where(o => value.SelectedOptions.Any(s => s.Id == o.Id))
                   .OrderBy(o => o.Order)
                   .Select(o => new FieldOptionResponse(o.Id, o.Label, o.Color, o.Order))
                   .ToList());

    private static SavedViewResponse ToResponse(
        SavedView view, Guid me, bool canManageShared, string? ownerNameOverride = null)
        => new(
            view.Id,
            view.ProjectId,
            view.Name,
            view.OwnerId,
            ownerNameOverride ?? view.Owner?.Name ?? string.Empty,
            view.IsShared,
            view.Order,
            view.SortBy,
            view.SortDescending,
            view.GroupBy,
            view.Filters
                .Select(f => new SavedViewFilterDto(f.Field, f.FieldDefinitionId, f.Operator, f.Value))
                .ToList(),
            view.Columns
                .OrderBy(c => c.Order)
                .Select(c => new SavedViewColumnDto(c.Field, c.FieldDefinitionId))
                .ToList(),
            CanEdit: view.OwnerId == me || (view.IsShared && canManageShared));
}
