using Microsoft.Extensions.Logging;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Extensions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Application.Features.CustomFields;
using PMS.Application.Features.Tasks;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.RequestPortal;

/// <summary>
/// Cổng yêu cầu — đường để người <b>ngoài</b> project gửi việc vào (ADR-063).
///
/// <para>
/// 🔴 <b>Lớp này KHÔNG gọi <c>IProjectAuthorizationService</c>, và đó là quyết định kiến
/// trúc của ADR-063, không phải một thiếu sót.</b> Đừng "sửa nó về cho nhất quán."
/// </para>
/// <para>
/// Lý do: <c>ProjectAuthorizationService</c> trả <b>404 cho người ngoài project</b> — cố ý,
/// vì 403 sẽ tiết lộ project đó tồn tại. Nhưng bản chất của một cổng tiếp nhận là người
/// ngoài gửi vào, nên gác nó bằng cơ chế đó là gác nó bằng chính thứ nó sinh ra để vượt qua.
/// </para>
/// <para>
/// Thay vào đó, <b>quyền nằm trong chính điều kiện truy vấn</b>:
/// <list type="bullet">
///   <item>đọc yêu cầu → vị từ <c>ReporterId == me</c> nằm CÙNG mệnh đề với <c>Id == taskId</c>;</item>
///   <item>gửi yêu cầu → loại việc phải có <c>IsRequestable</c>, tức chính đội xử lý đã mở cửa.</item>
/// </list>
/// Đây không phải một ngoại lệ mới: <c>TaskService.GetMyWorkAsync</c> (ADR-053) đã chạy
/// đúng khuôn này trong production, và XML doc của nó nói thẳng — <i>"Quyền nằm trong chính
/// điều kiện truy vấn, không phải trong một lượt kiểm thêm."</i> Dự án nay có <b>bốn</b>
/// ngoại lệ có chủ đích của mô hình hai tầng: <c>Notification</c> (ADR-023),
/// <c>ApproverMode</c> (ADR-062), <c>GetMyWorkAsync</c> (ADR-053), và lớp này.
/// </para>
/// <para>
/// ⚠️ Hệ quả bắt buộc nhớ: <b>mọi DTO ra khỏi lớp này đi tới một người có thể không thuộc
/// project nào</b>. Thêm một trường vào chúng là một quyết định bảo mật, không phải một
/// tiện ích hiển thị. Xem guard G4 ở <see cref="RequestPortalProjectResponse"/>.
/// </para>
/// </summary>
public class RequestPortalService : IRequestPortalService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly INotificationService _notifications;
    private readonly ILogger<RequestPortalService> _logger;

    public RequestPortalService(
        IUnitOfWork uow, ICurrentUserService currentUser, IActivityLogger activityLog,
        INotificationService notifications, ILogger<RequestPortalService> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _notifications = notifications;
        _logger = logger;
    }

    // ---------- (1) danh mục cổng ----------

    public async Task<IReadOnlyList<RequestPortalProjectResponse>> ListPortalsAsync(
        CancellationToken ct = default)
    {
        // Không lọc theo membership — xem XML doc của lớp và của ListRequestableAsync.
        // Người gọi chỉ cần đã đăng nhập; danh mục này là công khai trong nội bộ công ty,
        // đúng như một trang "gửi yêu cầu tới phòng ban nào" phải là.
        var types = await _uow.WorkItemTypes.ListRequestableAsync(ct);

        return types
            .GroupBy(t => new { t.ProjectId, t.Project.Name, t.Project.Key })
            .OrderBy(g => g.Key.Name)
            .Select(g => new RequestPortalProjectResponse(
                g.Key.ProjectId,
                g.Key.Name,
                g.Key.Key,
                g.OrderBy(t => t.Order).ThenBy(t => t.Id)
                 .Select(t => new RequestPortalTypeSummary(t.Id, t.Name, t.Icon, t.Color))
                 .ToList()))
            .ToList();
    }

    // ---------- (2) lược đồ form ----------

    public async Task<RequestPortalFormResponse> GetFormAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var types = await _uow.WorkItemTypes.ListRequestableByProjectAsync(projectId, ct);

        // 🔴 404 khi project không mở cổng — KHÔNG 403, và không phải một form rỗng.
        //
        // Một form rỗng sẽ xác nhận "project này tồn tại nhưng không nhận yêu cầu", tức vẫn
        // là rò rỉ, chỉ lịch sự hơn. 404 gộp ba ca — project không tồn tại · đã xoá mềm ·
        // chưa mở cổng — thành một câu trả lời duy nhất, đúng khuôn ADR-019.
        if (types.Count == 0)
            throw new NotFoundException(nameof(Project), projectId);

        var project = types[0].Project;

        return new RequestPortalFormResponse(
            projectId,
            project.Name,
            project.Key,
            types.Select(t => new RequestPortalTypeForm(
                t.Id, t.Name, t.Icon, t.Color,
                Normalize(t.RequestInstructions),
                t.Fields
                 .OrderBy(f => f.Order)
                 .Select(f => new RequestPortalFieldSchema(
                     f.FieldDefinitionId,
                     f.FieldDefinition.Label,
                     f.FieldDefinition.Type,
                     f.IsRequired,
                     f.Order,
                     f.FieldDefinition.Options
                      .OrderBy(o => o.Order).ThenBy(o => o.Id)
                      .Select(o => new FieldOptionResponse(o.Id, o.Label, o.Color, o.Order))
                      .ToList()))
                 .ToList()))
             .ToList());
    }

    // ---------- (3) gửi yêu cầu ----------

    public async Task<MyRequestResponse> SubmitAsync(
        Guid projectId, SubmitRequestRequest request, CancellationToken ct = default)
    {
        var me = _currentUser.RequireEmployeeId();

        // ----- G1: loại việc phải MỞ CỔNG -----
        //
        // Đây là điểm cưỡng chế duy nhất của cờ IsRequestable. Không có nó thì cờ đó là một
        // trường chết đội lốt tính năng — luật 4 Doctrine §0, tiền lệ Project.Status
        // (ADR-048). Có mutation test canh: gỡ phép kiểm này → test đỏ.
        //
        // 404 chứ không 403: id của một loại KHÔNG requestable, hoặc của project khác, đều
        // trả cùng một câu — không xác nhận nó tồn tại ở đâu.
        var type = await _uow.WorkItemTypes.GetWithFieldsAsync(request.WorkItemTypeId, ct);

        if (type is null || type.ProjectId != projectId || !type.IsRequestable)
            throw new NotFoundException(nameof(WorkItemType), request.WorkItemTypeId);

        // Cột trái nhất của project (ADR-052). Người gửi không chọn — và không được chọn:
        // cột là ngôn ngữ quy trình của đội xử lý, không phải của người gửi.
        var targetColumn = await _uow.BoardColumns.GetDefaultForProjectAsync(projectId, ct)
            ?? throw new NotFoundException(nameof(BoardColumn), projectId);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = Normalize(request.Description),
            ProjectId = projectId,
            // ----- G5 -----
            // ReporterId = người gửi. Đây là thứ MỌI phép đọc về sau lọc theo, nên nó vừa là
            // dữ liệu vừa là ranh giới quyền — không phải một trường thông tin.
            ReporterId = me,
            DueDate = request.DueDate,
            Priority = request.Priority,
            StoryPoints = 0,
            WorkItemTypeId = type.Id,
            // 🔴 Gán CẢ navigation, không chỉ khoá ngoại: mapper đọc `task.WorkItemType.Name`
            // ngay cuối hàm này, mà entity vừa dựng trong bộ nhớ thì EF chưa nạp hộ.
            // Đúng bẫy đã trả giá ở ADR-060.
            WorkItemType = type,
        };

        task.MoveTo(targetColumn);

        // ----- G2: trường bắt buộc phải ĐIỀN ĐỦ, cưỡng chế NGAY LÚC GỬI -----
        //
        // 🔴 Đây là điểm cưỡng chế MỚI, không phải chỗ đã có. `IsRequired` tới trước ADR-063
        // chỉ chặn ở CustomFieldService (khoảnh khắc người dùng XOÁ một giá trị), nên tạo
        // task mà bỏ trống trường bắt buộc vẫn đi lọt.
        //
        // ⚠️ KHÔNG đem phép kiểm này về POST /tasks. Comment ở CustomFieldService giải thích
        // vì sao: bật cờ bắt buộc cho một trường sẽ biến hàng trăm task cũ thành không hợp
        // lệ, và người dùng không sửa được thứ gì khác cho tới khi điền xong cái họ không
        // biết là đang thiếu. "Chặn hành động thì hẹp đúng mức có nghĩa; chặn cả bản ghi thì
        // biến một cấu hình thành một bức tường."
        //
        // Gửi yêu cầu là chỗ DUY NHẤT mà "điền đủ" chính là điểm của thao tác: không có bản
        // ghi cũ nào để làm hỏng, và người gửi đang nhìn thẳng vào cái form.
        var definitions = (await _uow.FieldDefinitions
                .ListByProjectWithTrackingAsync(projectId, ct))
            .ToDictionary(d => d.Id);

        var submitted = (request.FieldValues ?? [])
            .GroupBy(v => v.FieldDefinitionId)
            .ToDictionary(g => g.Key, g => g.Last());

        var fieldValues = new List<FieldValue>();

        foreach (var link in type.Fields.OrderBy(f => f.Order))
        {
            if (!definitions.TryGetValue(link.FieldDefinitionId, out var definition))
                // Loại khai một trường không thuộc project → dữ liệu đã hỏng, không phải
                // đầu vào xấu. Nói thẳng thay vì bỏ qua để nó không thành một ô mất tích.
                throw new NotFoundException(
                    $"Trường tuỳ biến {link.FieldDefinitionId} không thuộc project của loại việc này.");

            submitted.TryGetValue(definition.Id, out var item);

            var selectedIds = definition.IsSelect
                // Dùng CHUNG phép kiểm với CustomFieldService (internal từ ADR-063) thay vì
                // chép lại — hai nơi cùng dựng một luật là lớp lỗi ADR-034 đã đặt tên.
                ? CustomFieldService.ResolveOptionIds(definition, item?.SelectedOptionIds)
                : [];

            var isEmpty = definition.IsSelect
                ? selectedIds.Count == 0
                : item is null
                  || (item.ValueText is null && item.ValueNumber is null
                      && item.ValueDate is null && item.ValueBoolean is null);

            if (isEmpty)
            {
                if (link.IsRequired)
                    throw new BusinessRuleException(
                        $"Trường '{definition.Label}' là bắt buộc — vui lòng điền trước khi gửi yêu cầu.");

                // Không tạo hàng rỗng: ADR-059 xoá hẳn hàng khi không có giá trị, chính để
                // mọi phép đếm "bao nhiêu task đã điền trường này" trả lời đúng.
                continue;
            }

            var value = new FieldValue
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                FieldDefinitionId = definition.Id,
            };

            value.Set(definition.Type, item!.ValueText, item.ValueNumber,
                      item.ValueDate, item.ValueBoolean);

            if (definition.IsSelect)
                foreach (var option in definition.Options.Where(o => selectedIds.Contains(o.Id)))
                    value.SelectedOptions.Add(option);

            fieldValues.Add(value);
        }

        // ⚠️ Giá trị người gửi bắn kèm cho trường mà loại KHÔNG khai bị bỏ qua im lặng —
        // khác hẳn CustomFieldService (ném 404). Cố ý: ở đây form do server dựng, nên một id
        // lạ nghĩa là client cũ hoặc người dùng nghịch DevTools, không phải một ô đang mất
        // tích. Ném lỗi sẽ chặn một yêu cầu hợp lệ vì một trường không ai nhìn thấy.

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            task.AssignNumber(await _uow.ProjectTaskCounters.NextNumberAsync(projectId, ct));

            await _uow.Tasks.AddAsync(task, ct);

            foreach (var value in fieldValues)
                await _uow.FieldValues.AddAsync(value, ct);

            _activityLog.Log(nameof(TaskItem), task.Id, ActivityAction.RequestSubmitted,
                $"Gửi yêu cầu '{task.Name}' (loại {type.Name}) qua cổng tiếp nhận");

            await _uow.SaveChangesAsync(ct);
        }, ct);

        await NotifyManagersAsync(projectId, task, type, ct);

        var projectKey = await _uow.Projects.GetKeyAsync(projectId, ct)
            ?? throw new NotFoundException(nameof(Project), projectId);

        _logger.LogInformation(
            "Yêu cầu {TaskCode} ({TaskId}) gửi vào project {ProjectId} bởi {EmployeeId} (loại {TypeName})",
            TaskMapper.FormatCode(projectKey, task.Number), task.Id, projectId, me, type.Name);

        return ToSummary(task, projectKey);
    }

    // ---------- (4) yêu cầu của tôi ----------

    public async Task<PagedResult<MyRequestResponse>> ListMyRequestsAsync(
        PagedRequest request, CancellationToken ct = default)
    {
        var me = _currentUser.RequireEmployeeId();

        var page = await _uow.Tasks.GetRequestsByReporterAsync(
            me, request.Page, request.PageSize, ct);

        return page.Map(t => ToSummary(t, t.Project.Key));
    }

    // ---------- (5) chi tiết một yêu cầu ----------

    public async Task<MyRequestDetailResponse> GetMyRequestAsync(
        Guid taskId, CancellationToken ct = default)
    {
        var me = _currentUser.RequireEmployeeId();

        // ----- G3 -----
        // Repository nhét `ReporterId == me` vào CÙNG một vị từ với `Id == taskId`, nên
        // "không tồn tại" và "không phải của bạn" trả về đúng một câu trả lời. Tách thành
        // hai bước sẽ sinh cám dỗ ném 403 ở bước hai — và 403 xác nhận id đó có thật.
        var task = await _uow.Tasks.GetRequestForReporterAsync(taskId, me, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        var linked = task.WorkItemType.Fields.ToDictionary(f => f.FieldDefinitionId);

        var values = task.WorkItemType.Fields
            .OrderBy(f => f.Order)
            .Select(f =>
            {
                var value = task.FieldValues.FirstOrDefault(v => v.FieldDefinitionId == f.FieldDefinitionId);
                var definition = f.FieldDefinition;

                return new FieldValueResponse(
                    definition.Id, definition.Label, definition.Type,
                    linked.TryGetValue(definition.Id, out var link) && link.IsRequired,
                    value?.ValueText, value?.ValueNumber, value?.ValueDate, value?.ValueBoolean,
                    value is null
                        ? []
                        : definition.Options
                            .Where(o => value.SelectedOptions.Any(s => s.Id == o.Id))
                            .OrderBy(o => o.Order).ThenBy(o => o.Id)
                            .Select(o => new FieldOptionResponse(o.Id, o.Label, o.Color, o.Order))
                            .ToList());
            })
            .ToList();

        return new MyRequestDetailResponse(
            task.Id,
            TaskMapper.FormatCode(task.Project.Key, task.Number),
            task.Name,
            task.Description,
            task.ProjectId,
            task.Project.Name,
            task.WorkItemType.Name,
            task.WorkItemType.Icon,
            task.WorkItemType.Color,
            task.BoardColumn.Name,
            task.BoardColumn.Color,
            task.Category,
            task.Priority,
            task.DueDate,
            task.CreatedAt,
            ProjectApprovalState(task),
            values);
    }

    // ---------- phần dùng chung ----------

    private static MyRequestResponse ToSummary(TaskItem task, string projectKey)
        => new(
            task.Id,
            TaskMapper.FormatCode(projectKey, task.Number),
            task.Name,
            task.ProjectId,
            task.Project.Name,
            task.WorkItemType.Name,
            task.WorkItemType.Icon,
            task.WorkItemType.Color,
            task.BoardColumn.Name,
            task.BoardColumn.Color,
            task.Category,
            task.Priority,
            task.DueDate,
            task.CreatedAt,
            ProjectApprovalState(task));

    /// <summary>
    /// Chiếu các hàng <see cref="Approval"/> còn hiệu lực xuống một
    /// <see cref="TaskApprovalState"/> duy nhất.
    ///
    /// <para>
    /// 🔴 Cùng vị từ <c>ConsumedAt == null</c> với <c>TaskStatusTransitionService</c> và với
    /// nhánh lọc <c>TaskField.ApprovalState</c> ở <c>TaskRepository</c>. <b>Ba nơi phải nhìn
    /// cùng một tập hàng</b>, nếu không thì màn hình nói khác thứ cổng cưỡng chế.
    /// </para>
    /// <para>
    /// Thứ tự ưu tiên khi có nhiều hàng còn hiệu lực: Rejected → Pending → Approved. Người
    /// gửi cần thấy tin xấu trước, và một lời từ chối vẫn đang chặn thì đó mới là thứ họ
    /// phải hành động (ADR-062 quyết định c).
    /// </para>
    /// </summary>
    private static TaskApprovalState ProjectApprovalState(TaskItem task)
    {
        var live = task.Approvals.Where(a => a.ConsumedAt is null).ToList();

        if (live.Count == 0) return TaskApprovalState.None;
        if (live.Any(a => a.Status == ApprovalStatus.Rejected)) return TaskApprovalState.Rejected;
        if (live.Any(a => a.Status == ApprovalStatus.Pending))  return TaskApprovalState.Pending;
        if (live.Any(a => a.Status == ApprovalStatus.Approved)) return TaskApprovalState.Approved;

        // Chỉ còn hàng Cancelled — nhưng huỷ ĐÃ đặt ConsumedAt, nên nhánh này không tới
        // được qua đường nghiệp vụ. Trả None thay vì ném: một màn hình chỉ-đọc không phải
        // chỗ để phát hiện dữ liệu hỏng.
        return TaskApprovalState.None;
    }

    /// <summary>
    /// Báo cho PM của project rằng có yêu cầu mới.
    ///
    /// <para>
    /// 🔴 Đây là thông báo ĐẦU TIÊN trong hệ thống mà <b>người gây ra nó nằm ngoài
    /// project</b>. Vì vậy danh sách người nhận phải đọc từ <c>ProjectMembers</c>, không
    /// suy từ ngữ cảnh người gọi (người gọi không có ngữ cảnh nào ở project này).
    /// </para>
    /// <para>
    /// ⚠️ Gửi cho <b>PM đang hoạt động</b> (<c>Accepted</c>) thôi, không gửi cho cả đội: một
    /// yêu cầu mới là việc cần phân công, và phân công là việc của PM. Gửi cho mọi thành
    /// viên sẽ biến chuông thành nhiễu ngay tuần đầu.
    /// </para>
    /// </summary>
    private async Task NotifyManagersAsync(
        Guid projectId, TaskItem task, WorkItemType type, CancellationToken ct)
    {
        var project = await _uow.Projects.GetWithMembersAsync(projectId, ct);
        if (project is null) return;

        var managerIds = project.Members
            .Where(m => m.RoleInProject == RoleInProject.ProjectManager
                     && m.InvitationStatus == InvitationStatus.Accepted)
            .Select(m => m.EmployeeId)
            // Người gửi có thể TÌNH CỜ cũng là PM của chính project đó (không có gì cấm một
            // PM dùng cổng của đội mình). Tự báo cho mình là nhiễu thuần tuý.
            .Where(id => id != task.ReporterId)
            .Distinct()
            .ToList();

        if (managerIds.Count == 0) return;

        _notifications.NotifyMany(
            managerIds,
            NotificationType.RequestSubmitted,
            $"Yêu cầu mới: '{task.Name}' (loại {type.Name})",
            task.Id);

        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>Chuỗi rỗng/toàn khoảng trắng lưu thành <c>null</c> — cùng khuôn TaskService.</summary>
    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
