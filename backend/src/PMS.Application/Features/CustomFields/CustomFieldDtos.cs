using PMS.Domain.Enums;

namespace PMS.Application.Features.CustomFields;

// ---------- khai báo trường (schema) ----------

public record FieldOptionResponse(Guid Id, string Label, string Color, int Order);

public record FieldDefinitionResponse(
    Guid Id,
    Guid ProjectId,
    string Label,
    FieldType Type,
    int Order,
    IReadOnlyList<FieldOptionResponse> Options,
    // Số task đang có giá trị cho trường này — UI dùng để cảnh báo trước khi xoá.
    int ValueCount);

public record FieldOptionRequest(string Label, string Color);

/// <summary><paramref name="Options"/> chỉ có nghĩa với SingleSelect/MultiSelect.</summary>
public record CreateFieldDefinitionRequest(
    string Label, FieldType Type, IReadOnlyList<FieldOptionRequest>? Options = null);

/// <summary>
/// 🔴 KHÔNG có <c>Type</c>: kiểu không đổi được sau khi tạo. Xem ADR-059 — đổi kiểu nghĩa
/// là mọi giá trị đã nhập nằm sai cột, và không có phép chuyển nào đúng cho mọi dữ liệu
/// (một trường Text chứa "cao/thấp" không map được sang Number).
/// </summary>
public record UpdateFieldDefinitionRequest(
    string Label, IReadOnlyList<FieldOptionRequest>? Options = null);

public record ReorderFieldDefinitionsRequest(IReadOnlyList<Guid> FieldIds);

// ---------- giá trị trên task ----------

public record FieldValueResponse(
    Guid FieldDefinitionId,
    string Label,
    FieldType Type,
    string? ValueText,
    decimal? ValueNumber,
    DateTime? ValueDate,
    bool? ValueBoolean,
    IReadOnlyList<FieldOptionResponse> SelectedOptions);

/// <summary>
/// Một giá trị client gửi lên. Chỉ trường ứng với <c>Type</c> được đọc, phần còn lại bỏ qua
/// — client không phải biết cột nào là cột nào, và gửi thừa cũng không làm hỏng gì.
/// <para>
/// Mọi trường đều nullable: gửi tất cả null = XOÁ giá trị của trường đó.
/// </para>
/// </summary>
public record SetFieldValueRequest(
    Guid FieldDefinitionId,
    string? ValueText = null,
    decimal? ValueNumber = null,
    DateTime? ValueDate = null,
    bool? ValueBoolean = null,
    IReadOnlyList<Guid>? SelectedOptionIds = null);

/// <summary>
/// ⚠️ Đây là ghi kiểu PATCH, KHÔNG phải PUT: chỉ những trường có mặt trong
/// <paramref name="Values"/> bị đụng tới. Trường không nhắc tới giữ nguyên giá trị cũ.
/// <para>
/// Chọn vậy vì UI lưu từng ô một khi người dùng rời ô (giống mô tả task), nên một request
/// mang trọn bộ giá trị sẽ biến mỗi lần gõ thành một cơ hội ghi đè công của người khác.
/// </para>
/// </summary>
public record SetFieldValuesRequest(IReadOnlyList<SetFieldValueRequest> Values);
