namespace PMS.Application.Features.WorkItemTypes;

/// <summary>Một trường mà loại này khai dùng, kèm cờ bắt buộc.</summary>
public record WorkItemTypeFieldResponse(
    Guid FieldDefinitionId, string Label, bool IsRequired, int Order);

public record WorkItemTypeResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Icon,
    string Color,
    int Order,
    IReadOnlyList<WorkItemTypeFieldResponse> Fields,
    // Số task đang mang loại này — UI dùng để bắt chọn loại đích trước khi xoá.
    int TaskCount);

public record WorkItemTypeFieldRequest(Guid FieldDefinitionId, bool IsRequired);

/// <summary>
/// <paramref name="Fields"/> là danh sách ĐẦY ĐỦ trường mà loại dùng, theo đúng thứ tự
/// muốn hiển thị. Bỏ trống = loại không có trường tuỳ biến nào.
/// </summary>
public record CreateWorkItemTypeRequest(
    string Name, string Icon, string Color, IReadOnlyList<WorkItemTypeFieldRequest>? Fields = null);

public record UpdateWorkItemTypeRequest(
    string Name, string Icon, string Color, IReadOnlyList<WorkItemTypeFieldRequest>? Fields = null);

public record ReorderWorkItemTypesRequest(IReadOnlyList<Guid> TypeIds);

/// <summary>
/// Thân của <c>DELETE /work-item-types/{id}</c>.
///
/// <para>
/// ⚠️ <b>DELETE có thân request</b> — khác thường nhưng cố ý, y hệt
/// <c>DELETE /columns/{id}</c> (ADR-052): loại còn task thì bắt buộc kèm
/// <paramref name="TargetTypeId"/>. Đưa lên query string sẽ khiến một thao tác phá huỷ phụ
/// thuộc vào chuỗi URL — thứ dễ sao chép nhầm và nằm lại trong log máy chủ.
/// </para>
/// </summary>
public record DeleteWorkItemTypeRequest(Guid? TargetTypeId = null);
