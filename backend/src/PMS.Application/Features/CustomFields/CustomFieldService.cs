using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.CustomFields;

/// <summary>
/// Trường tuỳ biến theo project (ADR-059) — khai báo và giá trị.
///
/// <para>
/// Hai nửa có hai mức quyền KHÁC nhau, và đó là điểm thiết kế chính của lớp này:
/// sửa <b>khai báo</b> (thêm/xoá/đổi tên trường) là đổi cấu hình cả đội nhìn thấy nên cần
/// <see cref="ProjectAction.ManageFieldDefinitions"/> (PM); nhập <b>giá trị</b> là công
/// việc thường ngày trên một task nên đi cùng <see cref="ProjectAction.UpdateTask"/>.
/// Gộp hai thứ vào một quyền sẽ hoặc cấm Member nhập liệu, hoặc cho Member đổi lược đồ.
/// </para>
/// </summary>
public class CustomFieldService : ICustomFieldService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly IActivityLogger _activityLog;
    private readonly ILogger<CustomFieldService> _logger;

    public CustomFieldService(
        IUnitOfWork uow, IProjectAuthorizationService authz,
        IActivityLogger activityLog, ILogger<CustomFieldService> logger)
    {
        _uow = uow;
        _authz = authz;
        _activityLog = activityLog;
        _logger = logger;
    }

    // ---------- khai báo ----------

    public async Task<IReadOnlyList<FieldDefinitionResponse>> ListAsync(
        Guid projectId, CancellationToken ct = default)
    {
        // View chứ không phải ManageFieldDefinitions: Member và Viewer phải ĐỌC được lược đồ
        // thì mới hiển thị được giá trị trên task. Ẩn đi sẽ làm khối trường tuỳ biến trống
        // rỗng với họ mà không có lý do nào giải thích được — đúng lỗi ADR-052 đã tránh.
        await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);

        var fields = await _uow.FieldDefinitions.ListByProjectAsync(projectId, ct);
        var counts = await _uow.FieldDefinitions.CountValuesByFieldAsync(projectId, ct);

        return fields.Select(f => ToResponse(f, counts.GetValueOrDefault(f.Id))).ToList();
    }

    public async Task<FieldDefinitionResponse> CreateAsync(
        Guid projectId, CreateFieldDefinitionRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.ManageFieldDefinitions, ct);

        var label = request.Label.Trim();
        var existing = await _uow.FieldDefinitions.ListByProjectAsync(projectId, ct);

        // Kiểm ở đây thay vì để unique index ném: index trả DbUpdateException -> 500, còn
        // người dùng thì cần biết chính xác cái gì trùng. Cùng khuôn BoardColumnService.
        if (existing.Any(f => string.Equals(f.Label, label, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"Project đã có trường tên '{label}'.");

        var field = new FieldDefinition
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Label = label,
            Type = request.Type,
            // Thêm vào CUỐI. Chèn giữa phải dịch mọi trường phía sau, mà muốn vị trí khác
            // thì đã có thao tác sắp xếp lại.
            Order = existing.Count == 0 ? 0 : existing.Max(f => f.Order) + 1,
        };

        ApplyOptions(field, request.Options);

        await _uow.FieldDefinitions.AddAsync(field, ct);

        _activityLog.Log(nameof(Project), projectId, ActivityAction.Updated,
            $"Thêm trường tuỳ biến '{field.Label}' ({field.Type})");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Thêm trường {FieldId} '{Label}' ({Type}) vào project {ProjectId}",
            field.Id, field.Label, field.Type, projectId);

        return ToResponse(field, valueCount: 0);
    }

    public async Task<FieldDefinitionResponse> UpdateAsync(
        Guid fieldId, UpdateFieldDefinitionRequest request, CancellationToken ct = default)
    {
        var field = await RequireFieldAsync(fieldId, ProjectAction.ManageFieldDefinitions, ct);

        var label = request.Label.Trim();
        var siblings = await _uow.FieldDefinitions.ListByProjectAsync(field.ProjectId, ct);

        if (siblings.Any(f => f.Id != fieldId
                           && string.Equals(f.Label, label, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"Project đã có trường tên '{label}'.");

        field.Label = label;
        ApplyOptions(field, request.Options);

        _activityLog.Log(nameof(Project), field.ProjectId, ActivityAction.Updated,
            $"Sửa trường tuỳ biến '{field.Label}'");

        await _uow.SaveChangesAsync(ct);

        var counts = await _uow.FieldDefinitions.CountValuesByFieldAsync(field.ProjectId, ct);
        return ToResponse(field, counts.GetValueOrDefault(field.Id));
    }

    public async Task DeleteAsync(Guid fieldId, CancellationToken ct = default)
    {
        var field = await RequireFieldAsync(fieldId, ProjectAction.ManageFieldDefinitions, ct);

        // 🔴 KHÔNG hỏi "chuyển giá trị đi đâu" như dialog xoá cột board. Hai tình huống khác
        // nhau về bản chất: một task BẮT BUỘC đứng ở một cột nào đó, nên xoá cột phải có
        // cột đích. Còn một task không có giá trị cho một trường là trạng thái hoàn toàn
        // hợp lệ — chính là trạng thái của mọi task trước khi trường đó được tạo ra.
        // Xoá giá trị TRƯỚC, tường minh: FK từ FieldValues là Restrict (xem
        // FieldValueConfiguration — Cascade ở đó tạo hai đường cascade xuống bảng nối và
        // SQL Server từ chối tạo FK). Bỏ dòng này thì DELETE trường ném FK violation.
        var removedValues = await _uow.FieldDefinitions.DeleteValuesOfFieldAsync(fieldId, ct);

        _uow.FieldDefinitions.Remove(field);

        _activityLog.Log(nameof(Project), field.ProjectId, ActivityAction.Updated,
            $"Xoá trường tuỳ biến '{field.Label}' và {removedValues} giá trị của nó");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Xoá trường {FieldId} '{Label}' khỏi project {ProjectId}",
            field.Id, field.Label, field.ProjectId);
    }

    public async Task ReorderAsync(
        Guid projectId, ReorderFieldDefinitionsRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.ManageFieldDefinitions, ct);

        var fields = await _uow.FieldDefinitions.ListByProjectAsync(projectId, ct);
        var ids = request.FieldIds;

        // Đòi danh sách ĐẦY ĐỦ chứ không nhận danh sách một phần: nhận một phần thì phải
        // định nghĩa "phần còn lại đi đâu", và mọi câu trả lời đều là một luật ngầm mà
        // client phải đoán. Cùng khuôn với PUT /columns/order.
        if (ids.Count != fields.Count || ids.Distinct().Count() != ids.Count
            || ids.Any(id => fields.All(f => f.Id != id)))
            throw new BusinessRuleException(
                "Danh sách sắp xếp phải chứa ĐÚNG MỘT LẦN mỗi trường hiện có của project.");

        var tracked = new List<FieldDefinition>();
        for (var i = 0; i < ids.Count; i++)
        {
            var field = await _uow.FieldDefinitions.GetByIdAsync(ids[i], ct);
            if (field is null) continue;
            field.Order = i;
            tracked.Add(field);
        }

        _activityLog.Log(nameof(Project), projectId, ActivityAction.Updated,
            $"Đổi thứ tự {tracked.Count} trường tuỳ biến");

        await _uow.SaveChangesAsync(ct);
    }

    // ---------- giá trị ----------

    public async Task<IReadOnlyList<FieldValueResponse>> GetValuesAsync(
        Guid taskId, CancellationToken ct = default)
    {
        var task = await RequireTaskAsync(taskId, ProjectAction.View, ct);
        return await BuildValuesAsync(task.Id, task.ProjectId, ct);
    }

    public async Task<IReadOnlyList<FieldValueResponse>> SetValuesAsync(
        Guid taskId, SetFieldValuesRequest request, CancellationToken ct = default)
    {
        var task = await RequireTaskAsync(taskId, ProjectAction.UpdateTask, ct);

        // CÓ tracking — bắt buộc. Xem chú thích dài ở ListByProjectWithTrackingAsync: gán
        // một FieldOption rời vào SelectedOptions làm EF INSERT lại chính hàng đó.
        var definitions = (await _uow.FieldDefinitions.ListByProjectWithTrackingAsync(task.ProjectId, ct))
            .ToDictionary(f => f.Id);
        var existing = (await _uow.FieldDefinitions.ListValuesForTaskAsync(taskId, ct))
            .ToDictionary(v => v.FieldDefinitionId);

        foreach (var item in request.Values)
        {
            if (!definitions.TryGetValue(item.FieldDefinitionId, out var definition))
                // 🔴 404 chứ không bỏ qua im lặng. Một id trường không thuộc project này
                // nghĩa là client đang gửi rác HOẶC đang gửi trường của project khác —
                // nuốt lặng cả hai sẽ làm người dùng thấy "đã lưu" trên một giá trị không
                // tồn tại ở đâu cả.
                throw new NotFoundException(
                    $"Trường tuỳ biến {item.FieldDefinitionId} không thuộc project của task này.");

            existing.TryGetValue(definition.Id, out var value);

            var selectedIds = definition.IsSelect
                ? ResolveOptionIds(definition, item.SelectedOptionIds)
                : [];

            var wantsEmpty = !definition.IsSelect
                ? item.ValueText is null && item.ValueNumber is null
                  && item.ValueDate is null && item.ValueBoolean is null
                : selectedIds.Count == 0;

            if (wantsEmpty)
            {
                // Xoá hẳn hàng thay vì giữ một hàng toàn null: hàng rỗng làm mọi phép đếm
                // ("bao nhiêu task đã điền trường này") trả lời sai.
                if (value is not null) _uow.FieldValues.Remove(value);
                continue;
            }

            if (value is null)
            {
                value = new FieldValue
                {
                    Id = Guid.NewGuid(),
                    TaskId = taskId,
                    FieldDefinitionId = definition.Id,
                };
                await _uow.FieldValues.AddAsync(value, ct);
            }

            value.Set(definition.Type, item.ValueText, item.ValueNumber,
                      item.ValueDate, item.ValueBoolean);

            if (definition.IsSelect)
            {
                value.SelectedOptions.Clear();
                foreach (var option in definition.Options.Where(o => selectedIds.Contains(o.Id)))
                    value.SelectedOptions.Add(option);
            }
        }

        _activityLog.Log(nameof(TaskItem), taskId, ActivityAction.Updated,
            $"Cập nhật {request.Values.Count} trường tuỳ biến");

        await _uow.SaveChangesAsync(ct);

        return await BuildValuesAsync(taskId, task.ProjectId, ct);
    }

    // ---------- private ----------

    /// <summary>
    /// Lọc và kiểm danh sách lựa chọn client gửi lên.
    /// <para>
    /// 🔴 Lựa chọn PHẢI thuộc đúng trường đó. Không kiểm thì bất kỳ ai cũng gán được một
    /// option của trường khác (kể cả trường của project khác) vào task này — dữ liệu vẫn
    /// ghi được vì bảng nối không biết gì về quan hệ đó, và giao diện sẽ hiện một chip
    /// không có trong danh sách của chính trường đang xem.
    /// </para>
    /// </summary>
    private static List<Guid> ResolveOptionIds(
        FieldDefinition definition, IReadOnlyList<Guid>? requested)
    {
        if (requested is null || requested.Count == 0) return [];

        var valid = definition.Options.Select(o => o.Id).ToHashSet();
        var unknown = requested.Where(id => !valid.Contains(id)).ToList();

        if (unknown.Count > 0)
            throw new BusinessRuleException(
                $"Lựa chọn {string.Join(", ", unknown)} không thuộc trường '{definition.Label}'.");

        var distinct = requested.Distinct().ToList();

        if (definition.Type == FieldType.SingleSelect && distinct.Count > 1)
            throw new BusinessRuleException(
                $"Trường '{definition.Label}' chỉ cho chọn một giá trị.");

        return distinct;
    }

    /// <summary>
    /// Đặt lại toàn bộ danh sách lựa chọn của một trường.
    /// <para>
    /// ⚠️ Khớp theo <b>Label</b> chứ không theo Id: request sửa trường không mang Id của
    /// option (client dựng danh sách bằng cách gõ chữ). Nhờ khớp theo tên, sửa màu hoặc đổi
    /// thứ tự KHÔNG làm mất giá trị các task đang trỏ tới option đó. Đổi TÊN một option thì
    /// đúng là mất — và đó là hành vi trung thực: về mặt dữ liệu nó là một lựa chọn khác.
    /// </para>
    /// </summary>
    private static void ApplyOptions(
        FieldDefinition field, IReadOnlyList<FieldOptionRequest>? requested)
    {
        if (!field.IsSelect)
        {
            // Kiểu không phải Select mà gửi kèm options: bỏ qua thay vì báo lỗi. Client đổi
            // kiểu trong form rồi submit là chuyện thường, và options thừa không hại gì.
            field.Options.Clear();
            return;
        }

        var options = requested ?? [];

        if (options.Count == 0)
            throw new BusinessRuleException(
                $"Trường kiểu {field.Type} phải có ít nhất một lựa chọn.");

        var labels = options.Select(o => o.Label.Trim()).ToList();
        if (labels.Distinct(StringComparer.OrdinalIgnoreCase).Count() != labels.Count)
            throw new BusinessRuleException("Các lựa chọn trong cùng một trường không được trùng tên.");

        var keep = new List<FieldOption>();
        for (var i = 0; i < options.Count; i++)
        {
            var label = labels[i];
            var existing = field.Options.FirstOrDefault(
                o => string.Equals(o.Label, label, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                existing.Color = options[i].Color.Trim();
                existing.Order = i;
                keep.Add(existing);
            }
            else
            {
                keep.Add(new FieldOption
                {
                    Id = Guid.NewGuid(),
                    FieldDefinitionId = field.Id,
                    Label = label,
                    Color = options[i].Color.Trim(),
                    Order = i,
                });
            }
        }

        // Gán lại cả tập: EF sinh DELETE cho phần tử biến mất (cascade xuống bảng nối).
        field.Options.Clear();
        foreach (var option in keep) field.Options.Add(option);
    }

    private async Task<FieldDefinition> RequireFieldAsync(
        Guid fieldId, ProjectAction action, CancellationToken ct)
    {
        var field = await _uow.FieldDefinitions.GetWithOptionsAsync(fieldId, ct)
            ?? throw new NotFoundException(nameof(FieldDefinition), fieldId);

        // Quyền kiểm theo PROJECT của trường, không theo id trường: người ngoài project phải
        // nhận 404 từ AuthorizeAsync (ADR-006), không phải 403 — 403 xác nhận id có thật.
        await _authz.AuthorizeAsync(field.ProjectId, action, ct);
        return field;
    }

    private async Task<TaskItem> RequireTaskAsync(
        Guid taskId, ProjectAction action, CancellationToken ct)
    {
        var task = await _uow.Tasks.GetByIdAsync(taskId, ct)
            ?? throw new NotFoundException(nameof(TaskItem), taskId);

        await _authz.AuthorizeAsync(task.ProjectId, action, ct);
        return task;
    }

    /// <summary>
    /// Trả về MỌI trường của project, kể cả trường task chưa điền (giá trị null).
    /// <para>
    /// Cố ý không chỉ trả các hàng có trong <c>FieldValues</c>: giao diện cần dựng đủ ô
    /// nhập, và bắt frontend tự gộp "danh sách trường" với "danh sách giá trị" là đẩy một
    /// phép join sang chỗ dễ làm sai hơn — đúng lớp lỗi ADR-034 đã đặt tên (hai nơi cùng
    /// dựng một thứ thì chắc chắn có lúc lệch).
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<FieldValueResponse>> BuildValuesAsync(
        Guid taskId, Guid projectId, CancellationToken ct)
    {
        var definitions = await _uow.FieldDefinitions.ListByProjectAsync(projectId, ct);
        var values = (await _uow.FieldDefinitions.ListValuesForTaskAsync(taskId, ct))
            .ToDictionary(v => v.FieldDefinitionId);

        return definitions.Select(d =>
        {
            values.TryGetValue(d.Id, out var v);
            return new FieldValueResponse(
                d.Id, d.Label, d.Type,
                v?.ValueText, v?.ValueNumber, v?.ValueDate, v?.ValueBoolean,
                v is null
                    ? []
                    : d.Options.Where(o => v.SelectedOptions.Any(s => s.Id == o.Id))
                               .Select(ToOptionResponse).ToList());
        }).ToList();
    }

    private static FieldOptionResponse ToOptionResponse(FieldOption o)
        => new(o.Id, o.Label, o.Color, o.Order);

    private static FieldDefinitionResponse ToResponse(FieldDefinition f, int valueCount)
        => new(f.Id, f.ProjectId, f.Label, f.Type, f.Order,
               f.Options.OrderBy(o => o.Order).ThenBy(o => o.Id).Select(ToOptionResponse).ToList(),
               valueCount);
}
