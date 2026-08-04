using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Application.Features.Tasks;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Labels;

/// <summary>
/// Nhãn là dữ liệu <b>toàn cục</b>, nên quyền tách theo BÁN KÍNH ẢNH HƯỞNG chứ không theo
/// cấp bậc (ADR-037) — cùng tinh thần ADR-026 tách quyền comment theo mức độ xâm phạm:
/// <list type="bullet">
/// <item>Tạo nhãn: mọi user đã đăng nhập. Thao tác cộng thêm, không ảnh hưởng ai.</item>
/// <item>Gắn/gỡ nhãn trên task: <c>ProjectAction.ManageTaskLabels</c> — phạm vi một project.</item>
/// <item>Sửa/xóa nhãn: <b>chỉ SystemAdmin</b> (gác bằng policy ở controller). Xóa nhãn
/// <c>urgent</c> là gỡ chip khỏi board của <b>mọi</b> project — không PM nào nên sở hữu
/// một tác dụng phụ xuyên project.</item>
/// </list>
/// Cách sửa gốc là nhãn theo project (<c>Label.ProjectId</c> + unique <c>(ProjectId, Name)</c>);
/// đã hoãn có ý thức vì cần thêm một migration dữ liệu cho bảng nối <c>TaskLabels</c>.
/// </summary>
public class LabelService : ILabelService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogger _activityLog;
    private readonly LabelMapper _mapper;
    private readonly ILogger<LabelService> _logger;

    public LabelService(
        IUnitOfWork uow, IProjectAuthorizationService authz, ICurrentUserService currentUser,
        IActivityLogger activityLog, LabelMapper mapper, ILogger<LabelService> logger)
    {
        _uow = uow;
        _authz = authz;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LabelResponse>> GetAllAsync(CancellationToken ct = default)
        => (await _uow.Labels.ListAllOrderedAsync(ct)).Select(_mapper.ToResponse).ToList();

    public async Task<LabelResponse> CreateAsync(
        CreateLabelRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();

        // Kiểm trước để trả 409 có thông điệp rõ ràng; unique index vẫn là chốt chặn cuối
        // cho race giữa hai request đồng thời.
        if (await _uow.Labels.NameExistsAsync(name, ct: ct))
            throw new ConflictException($"Đã có nhãn tên '{name}'.");

        var label = new Label
        {
            Id = Guid.NewGuid(),
            Name = name,
            Color = string.IsNullOrWhiteSpace(request.Color) ? Label.DefaultColor : request.Color
        };

        await _uow.Labels.AddAsync(label, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Tạo nhãn {LabelName} bởi {EmployeeId}", name, _currentUser.EmployeeId);

        return _mapper.ToResponse(label);
    }

    public async Task<LabelResponse> UpdateAsync(
        Guid id, UpdateLabelRequest request, CancellationToken ct = default)
    {
        var label = await _uow.Labels.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Label), id);

        var name = request.Name.Trim();
        if (await _uow.Labels.NameExistsAsync(name, id, ct))
            throw new ConflictException($"Đã có nhãn tên '{name}'.");

        var oldName = label.Name;
        label.Name = name;
        label.Color = request.Color;

        // Ghi ActivityLog với EntityType = Label: đây là hành động CẤP HỆ THỐNG, và là một
        // trong hai loại mà GET /admin/audit-logs được phép đọc (ADR-042).
        _activityLog.Log(nameof(Label), id, ActivityAction.Updated,
            $"Sửa nhãn toàn cục '{oldName}' -> '{name}' ({request.Color})");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Sửa nhãn {LabelId} bởi {EmployeeId}", id, _currentUser.EmployeeId);

        return _mapper.ToResponse(label);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // GetWithTasksAsync chứ không phải GetByIdAsync: EF cần collection Tasks đã nạp
        // mới gỡ được các dòng ở bảng nối TaskLabels. Thiếu Include thì xóa nhãn nổ FK.
        var label = await _uow.Labels.GetWithTasksAsync(id, ct)
            ?? throw new NotFoundException(nameof(Label), id);

        var affectedTasks = label.Tasks.Count;
        label.Tasks.Clear();
        _uow.Labels.Remove(label);

        _activityLog.Log(nameof(Label), id, ActivityAction.Deleted,
            $"Xóa nhãn toàn cục '{label.Name}' (đang gắn trên {affectedTasks} task)");

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Xóa nhãn toàn cục {LabelName} bởi {EmployeeId}: gỡ khỏi {TaskCount} task",
            label.Name, _currentUser.EmployeeId, affectedTasks);
    }

    public async Task<IReadOnlyList<LabelResponse>> AttachToTaskAsync(
        Guid taskId, Guid labelId, CancellationToken ct = default)
    {
        var (task, label) = await LoadAndAuthorizeAsync(taskId, labelId, ct);

        // Idempotent: gắn nhãn đã có sẵn không phải vi phạm nghiệp vụ (tiền lệ
        // Notification.MarkAsRead ở ADR-023). Client không cần dò trạng thái trước khi gọi.
        if (task.Labels.Any(l => l.Id == labelId))
            return task.Labels.Select(_mapper.ToResponse).ToList();

        task.Labels.Add(label);

        _activityLog.Log(nameof(TaskItem), taskId, ActivityAction.Updated,
            $"Gắn nhãn '{label.Name}' vào task '{task.Name}'");

        await _uow.SaveChangesAsync(ct);

        return task.Labels.Select(_mapper.ToResponse).ToList();
    }

    public async Task<IReadOnlyList<LabelResponse>> DetachFromTaskAsync(
        Guid taskId, Guid labelId, CancellationToken ct = default)
    {
        var (task, label) = await LoadAndAuthorizeAsync(taskId, labelId, ct);

        var attached = task.Labels.FirstOrDefault(l => l.Id == labelId);
        if (attached is null)
            return task.Labels.Select(_mapper.ToResponse).ToList();   // idempotent

        task.Labels.Remove(attached);

        _activityLog.Log(nameof(TaskItem), taskId, ActivityAction.Updated,
            $"Gỡ nhãn '{label.Name}' khỏi task '{task.Name}'");

        await _uow.SaveChangesAsync(ct);

        return task.Labels.Select(_mapper.ToResponse).ToList();
    }

    /// <summary>
    /// Nạp task (kèm Labels đã tracking để sửa được collection) + nhãn, và kiểm quyền.
    /// Thứ tự có chủ đích: kiểm quyền trên TASK trước, vì người ngoài project phải nhận
    /// 404 về task chứ không được biết labelId có tồn tại hay không (ADR-019).
    /// </summary>
    private async Task<(TaskItem Task, Label Label)> LoadAndAuthorizeAsync(
        Guid taskId, Guid labelId, CancellationToken ct)
    {
        var task = await _uow.Tasks.GetWithLabelsAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        await _authz.AuthorizeTaskAsync(task, ProjectAction.ManageTaskLabels, ct);

        var label = await _uow.Labels.GetByIdAsync(labelId, ct)
            ?? throw new NotFoundException(nameof(Label), labelId);

        return (task, label);
    }
}
