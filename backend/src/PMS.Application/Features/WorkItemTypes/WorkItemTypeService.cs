using Microsoft.Extensions.Logging;
using PMS.Application.Common.Authorization;
using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.WorkItemTypes;

public interface IWorkItemTypeService
{
    Task<IReadOnlyList<WorkItemTypeResponse>> ListAsync(Guid projectId, CancellationToken ct = default);
    Task<WorkItemTypeResponse> CreateAsync(
        Guid projectId, CreateWorkItemTypeRequest request, CancellationToken ct = default);
    Task<WorkItemTypeResponse> UpdateAsync(
        Guid typeId, UpdateWorkItemTypeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid typeId, DeleteWorkItemTypeRequest request, CancellationToken ct = default);
    Task ReorderAsync(
        Guid projectId, ReorderWorkItemTypesRequest request, CancellationToken ct = default);
}

/// <summary>
/// Loại công việc theo project (ADR-060).
///
/// <para>
/// Lớp này giữ một bất biến giống hệt <c>BoardColumnService</c>: <b>mọi task luôn thuộc
/// đúng một loại tồn tại</b>. Vì vậy không xoá được loại cuối cùng, và xoá một loại còn
/// task thì bắt buộc chọn loại đích.
/// </para>
/// </summary>
public class WorkItemTypeService : IWorkItemTypeService
{
    private readonly IUnitOfWork _uow;
    private readonly IProjectAuthorizationService _authz;
    private readonly IActivityLogger _activityLog;
    private readonly ILogger<WorkItemTypeService> _logger;

    public WorkItemTypeService(
        IUnitOfWork uow, IProjectAuthorizationService authz,
        IActivityLogger activityLog, ILogger<WorkItemTypeService> logger)
    {
        _uow = uow;
        _authz = authz;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WorkItemTypeResponse>> ListAsync(
        Guid projectId, CancellationToken ct = default)
    {
        // View: mọi thành viên phải đọc được danh sách loại, nếu không thì chip loại trên
        // thẻ Kanban của Viewer rỗng mà không có lý do nào giải thích được.
        await _authz.AuthorizeAsync(projectId, ProjectAction.View, ct);

        var types = await _uow.WorkItemTypes.ListByProjectAsync(projectId, ct);
        var counts = await _uow.WorkItemTypes.CountTasksByTypeAsync(projectId, ct);

        return types.Select(t => ToResponse(t, counts.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<WorkItemTypeResponse> CreateAsync(
        Guid projectId, CreateWorkItemTypeRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.ManageWorkItemTypes, ct);

        var name = request.Name.Trim();
        var existing = await _uow.WorkItemTypes.ListByProjectAsync(projectId, ct);

        if (existing.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"Project đã có loại công việc tên '{name}'.");

        var type = new WorkItemType
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            Icon = request.Icon.Trim(),
            Color = request.Color.Trim(),
            Order = existing.Count == 0 ? 0 : existing.Max(t => t.Order) + 1,
        };

        await ApplyFieldsAsync(type, projectId, request.Fields, ct);
        await _uow.WorkItemTypes.AddAsync(type, ct);

        _activityLog.Log(nameof(Project), projectId, ActivityAction.Updated,
            $"Thêm loại công việc '{type.Name}'");

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Thêm loại {TypeId} '{Name}' vào project {ProjectId}",
            type.Id, type.Name, projectId);

        return ToResponse(type, taskCount: 0);
    }

    public async Task<WorkItemTypeResponse> UpdateAsync(
        Guid typeId, UpdateWorkItemTypeRequest request, CancellationToken ct = default)
    {
        var type = await RequireTypeAsync(typeId, ct);

        var name = request.Name.Trim();
        var siblings = await _uow.WorkItemTypes.ListByProjectAsync(type.ProjectId, ct);

        if (siblings.Any(t => t.Id != typeId
                           && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"Project đã có loại công việc tên '{name}'.");

        type.Name = name;
        type.Icon = request.Icon.Trim();
        type.Color = request.Color.Trim();

        await ApplyFieldsAsync(type, type.ProjectId, request.Fields, ct);

        _activityLog.Log(nameof(Project), type.ProjectId, ActivityAction.Updated,
            $"Sửa loại công việc '{type.Name}'");

        await _uow.SaveChangesAsync(ct);

        var counts = await _uow.WorkItemTypes.CountTasksByTypeAsync(type.ProjectId, ct);
        return ToResponse(type, counts.GetValueOrDefault(type.Id));
    }

    public async Task DeleteAsync(
        Guid typeId, DeleteWorkItemTypeRequest request, CancellationToken ct = default)
    {
        var type = await RequireTypeAsync(typeId, ct);

        var siblings = await _uow.WorkItemTypes.ListByProjectAsync(type.ProjectId, ct);
        if (siblings.Count <= 1)
            throw new ConflictException(
                "Không xoá được loại công việc cuối cùng — mọi task đều phải thuộc một loại.");

        var counts = await _uow.WorkItemTypes.CountTasksByTypeAsync(type.ProjectId, ct);
        var taskCount = counts.GetValueOrDefault(typeId);

        if (taskCount > 0)
        {
            // Thông điệp có kèm SỐ task: người dùng cần biết quy mô của việc mình sắp làm,
            // không chỉ biết là "phải chọn thêm gì đó". Cùng khuôn xoá cột board.
            if (request.TargetTypeId is not { } targetId)
                throw new BusinessRuleException(
                    $"Loại này đang có {taskCount} task. Hãy chọn loại đích để chuyển chúng sang.");

            if (targetId == typeId)
                throw new BusinessRuleException("Loại đích phải khác loại đang xoá.");

            // Loại đích phải cùng project — không kiểm thì task lạc sang loại của project
            // khác, và mọi màn hình lọc theo loại sẽ im lặng bỏ sót chúng.
            if (siblings.All(t => t.Id != targetId))
                throw new NotFoundException(nameof(WorkItemType), targetId);

            await _uow.WorkItemTypes.MoveAllTasksAsync(typeId, targetId, ct);
        }

        _uow.WorkItemTypes.Remove(type);

        _activityLog.Log(nameof(Project), type.ProjectId, ActivityAction.Updated,
            taskCount > 0
                ? $"Xoá loại công việc '{type.Name}', chuyển {taskCount} task sang loại khác"
                : $"Xoá loại công việc '{type.Name}'");

        await _uow.SaveChangesAsync(ct);
    }

    public async Task ReorderAsync(
        Guid projectId, ReorderWorkItemTypesRequest request, CancellationToken ct = default)
    {
        await _authz.AuthorizeAsync(projectId, ProjectAction.ManageWorkItemTypes, ct);

        var types = await _uow.WorkItemTypes.ListByProjectAsync(projectId, ct);
        var ids = request.TypeIds;

        if (ids.Count != types.Count || ids.Distinct().Count() != ids.Count
            || ids.Any(id => types.All(t => t.Id != id)))
            throw new BusinessRuleException(
                "Danh sách sắp xếp phải chứa ĐÚNG MỘT LẦN mỗi loại hiện có của project.");

        for (var i = 0; i < ids.Count; i++)
        {
            var type = await _uow.WorkItemTypes.GetByIdAsync(ids[i], ct);
            if (type is not null) type.Order = i;
        }

        _activityLog.Log(nameof(Project), projectId, ActivityAction.Updated,
            $"Đổi thứ tự {ids.Count} loại công việc");

        await _uow.SaveChangesAsync(ct);
    }

    // ---------- private ----------

    /// <summary>
    /// Đặt lại toàn bộ tập trường của một loại.
    ///
    /// <para>
    /// 🔴 Mọi <c>FieldDefinitionId</c> phải thuộc ĐÚNG project của loại. Không kiểm thì PM
    /// gắn được trường của project khác vào loại này — và khi đó
    /// <c>GET /tasks/{id}/field-values</c> trả về một trường mà chính project không có,
    /// tức một ô nhập không thuộc về đâu cả.
    /// </para>
    /// </summary>
    private async Task ApplyFieldsAsync(
        WorkItemType type, Guid projectId,
        IReadOnlyList<WorkItemTypeFieldRequest>? requested, CancellationToken ct)
    {
        var fields = requested ?? [];

        if (fields.Count == 0)
        {
            type.Fields.Clear();
            return;
        }

        var ids = fields.Select(f => f.FieldDefinitionId).ToList();
        if (ids.Distinct().Count() != ids.Count)
            throw new BusinessRuleException("Một trường chỉ được gắn vào loại đúng một lần.");

        var valid = (await _uow.FieldDefinitions.ListByProjectAsync(projectId, ct))
            .Select(f => f.Id).ToHashSet();

        var unknown = ids.Where(id => !valid.Contains(id)).ToList();
        if (unknown.Count > 0)
            throw new NotFoundException(
                $"Trường tuỳ biến {string.Join(", ", unknown)} không thuộc project này.");

        type.Fields.Clear();
        for (var i = 0; i < fields.Count; i++)
            type.Fields.Add(new WorkItemTypeField
            {
                WorkItemTypeId = type.Id,
                FieldDefinitionId = fields[i].FieldDefinitionId,
                IsRequired = fields[i].IsRequired,
                Order = i,
            });
    }

    private async Task<WorkItemType> RequireTypeAsync(Guid typeId, CancellationToken ct)
    {
        var type = await _uow.WorkItemTypes.GetWithFieldsAsync(typeId, ct)
            ?? throw new NotFoundException(nameof(WorkItemType), typeId);

        // Quyền kiểm theo PROJECT của loại: người ngoài nhận 404 từ AuthorizeAsync (ADR-006),
        // không phải 403 — 403 xác nhận id có thật.
        await _authz.AuthorizeAsync(type.ProjectId, ProjectAction.ManageWorkItemTypes, ct);
        return type;
    }

    private static WorkItemTypeResponse ToResponse(WorkItemType type, int taskCount)
        => new(type.Id, type.ProjectId, type.Name, type.Icon, type.Color, type.Order,
               type.Fields
                   .OrderBy(f => f.Order)
                   .Select(f => new WorkItemTypeFieldResponse(
                       f.FieldDefinitionId,
                       f.FieldDefinition?.Label ?? string.Empty,
                       f.IsRequired,
                       f.Order))
                   .ToList(),
               taskCount);
}
