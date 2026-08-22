using System.Globalization;
using PMS.Application.Common.Exceptions;
using PMS.Domain.Enums;

namespace PMS.Application.Common.Filtering;

/// <summary>
/// Nguồn sự thật DUY NHẤT cho câu hỏi "trường này lọc được bằng toán tử nào, và giá trị người
/// dùng gõ có hợp lệ không" (ADR-061).
///
/// <para>
/// 🔑 <b>Luật đi theo KIỂU GIÁ TRỊ, không theo từng trường.</b> Mỗi trường — dựng sẵn hay
/// tuỳ biến — khai mình thuộc một <see cref="FilterValueKind"/>, và mỗi kiểu cho phép một tập
/// toán tử. Nhờ vậy thêm một trường dựng sẵn mới chỉ là thêm một nhánh ở
/// <see cref="KindOf(TaskField)"/>, không phải bảo trì một ma trận trường × toán tử — thứ
/// chắc chắn lệch sau vài lần sửa.
/// </para>
/// <para>
/// ⚠️ Mọi phép kiểm ở đây chạy lúc <b>GHI</b> view (400 kèm thông điệp đọc được), không phải
/// lúc chạy view. Một bộ lọc đã lưu rồi mới báo lỗi lúc mở là thứ người dùng không sửa được
/// — họ không biết mình đã lưu cái gì sai.
/// </para>
/// </summary>
public static class TaskFilterCatalog
{
    /// <summary>Kiểu giá trị của một trường DỰNG SẴN.</summary>
    public static FilterValueKind KindOf(TaskField field) => field switch
    {
        TaskField.Name         => FilterValueKind.Text,

        TaskField.BoardColumn
        or TaskField.WorkItemType
        or TaskField.Sprint
        or TaskField.Assignee
        or TaskField.Reporter  => FilterValueKind.Reference,

        TaskField.Category
        or TaskField.Priority  => FilterValueKind.Enum,

        TaskField.DueDate
        or TaskField.CreatedAt => FilterValueKind.Date,

        TaskField.StoryPoints  => FilterValueKind.Number,

        // Không dùng `_ =>`: một thành viên enum mới thêm vào TaskField mà quên khai kiểu sẽ
        // là lỗi BIÊN DỊCH ở đây thay vì một nhánh mặc định đoán sai lúc chạy.
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Trường chưa khai kiểu giá trị.")
    };

    /// <summary>Kiểu giá trị của một trường TUỲ BIẾN (ADR-059).</summary>
    public static FilterValueKind KindOf(FieldType type) => type switch
    {
        FieldType.Text or FieldType.Url          => FilterValueKind.Text,
        FieldType.Number                         => FilterValueKind.Number,
        FieldType.Date                           => FilterValueKind.Date,
        FieldType.Checkbox                       => FilterValueKind.Boolean,
        // Lọc theo Select là lọc theo LỰA CHỌN nào đang được chọn — giá trị là Id của
        // FieldOption, nên nó là một tham chiếu chứ không phải chuỗi. So theo nhãn sẽ vỡ ngay
        // lần đầu người dùng đổi tên một lựa chọn, đúng cái bẫy ADR-059 đã cảnh báo.
        FieldType.SingleSelect or FieldType.MultiSelect => FilterValueKind.Reference,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Kiểu trường chưa khai kiểu giá trị.")
    };

    /// <summary>Toán tử nào dùng được với kiểu giá trị nào.</summary>
    public static bool Supports(FilterValueKind kind, FilterOperator op) => op switch
    {
        // Có/không có giá trị: đúng với mọi kiểu.
        FilterOperator.IsEmpty or FilterOperator.IsNotEmpty => true,

        FilterOperator.Equals or FilterOperator.NotEquals => true,

        // Chỉ chuỗi mới "chứa" được.
        FilterOperator.Contains => kind is FilterValueKind.Text,

        // So sánh có thứ tự: chỉ số và ngày.
        //
        // 📌 Enum cố ý KHÔNG có: Priority.Highest = 0 nên "lớn hơn Medium" sẽ trả về những
        // việc ÍT ưu tiên hơn — đúng theo số, ngược hẳn với thứ người dùng đọc được từ chữ.
        // Một toán tử nói ngược nghĩa tệ hơn là không có toán tử đó (§0, "không ship một cờ
        // không chặn được gì" — ở đây là không ship một toán tử nói dối).
        FilterOperator.GreaterThan or FilterOperator.GreaterThanOrEqual
        or FilterOperator.LessThan or FilterOperator.LessThanOrEqual
            => kind is FilterValueKind.Number or FilterValueKind.Date,

        _ => false
    };

    /// <summary>
    /// Phân giải một dòng điều kiện thô thành <see cref="ResolvedFilter"/>, hoặc ném
    /// <see cref="BusinessRuleException"/> (→ 400) kèm thông điệp người dùng đọc được.
    /// </summary>
    /// <param name="field">Trường dựng sẵn — loại trừ lẫn nhau với <paramref name="fieldDefinitionId"/>.</param>
    /// <param name="fieldDefinitionId">Trường tuỳ biến.</param>
    /// <param name="customType">Kiểu của trường tuỳ biến; bắt buộc khi có <paramref name="fieldDefinitionId"/>.</param>
    /// <param name="label">Tên trường để đưa vào thông điệp lỗi.</param>
    public static ResolvedFilter Resolve(
        TaskField? field,
        Guid? fieldDefinitionId,
        FieldType? customType,
        FilterOperator op,
        string? rawValue,
        string label)
    {
        if (field.HasValue == fieldDefinitionId.HasValue)
            throw new BusinessRuleException(
                "Mỗi điều kiện phải nhắm vào ĐÚNG MỘT trong hai: một trường dựng sẵn hoặc " +
                "một trường tuỳ biến.");

        var kind = field.HasValue
            ? KindOf(field.Value)
            : KindOf(customType ?? throw new BusinessRuleException(
                $"Không xác định được kiểu của trường tuỳ biến '{label}'."));

        if (!Supports(kind, op))
            throw new BusinessRuleException(
                $"Toán tử '{op}' không dùng được với trường '{label}'.");

        // IsEmpty/IsNotEmpty không mang giá trị. Nhận rồi bỏ qua im lặng sẽ làm người dùng
        // tưởng giá trị họ gõ có tác dụng.
        if (op is FilterOperator.IsEmpty or FilterOperator.IsNotEmpty)
            return new ResolvedFilter(field, fieldDefinitionId, kind, op);

        if (string.IsNullOrWhiteSpace(rawValue))
            throw new BusinessRuleException(
                $"Điều kiện trên trường '{label}' thiếu giá trị so sánh.");

        var value = rawValue.Trim();

        return kind switch
        {
            FilterValueKind.Text => new ResolvedFilter(
                field, fieldDefinitionId, kind, op, Text: value),

            FilterValueKind.Number => new ResolvedFilter(
                field, fieldDefinitionId, kind, op,
                Number: decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)
                    ? n
                    : throw new BusinessRuleException(
                        $"Giá trị '{value}' của trường '{label}' không phải một số.")),

            FilterValueKind.Date => new ResolvedFilter(
                field, fieldDefinitionId, kind, op, Date: ParseUtcDate(value, label)),

            FilterValueKind.Boolean => new ResolvedFilter(
                field, fieldDefinitionId, kind, op,
                Boolean: bool.TryParse(value, out var b)
                    ? b
                    : throw new BusinessRuleException(
                        $"Giá trị '{value}' của trường '{label}' phải là true hoặc false.")),

            FilterValueKind.Reference => new ResolvedFilter(
                field, fieldDefinitionId, kind, op,
                Reference: Guid.TryParse(value, out var g)
                    ? g
                    : throw new BusinessRuleException(
                        $"Giá trị của trường '{label}' phải là một định danh hợp lệ.")),

            FilterValueKind.Enum => new ResolvedFilter(
                field, fieldDefinitionId, kind, op,
                EnumValue: ParseEnumByName(field!.Value, value, label)),

            _ => throw new BusinessRuleException($"Không xử lý được trường '{label}'.")
        };
    }

    /// <summary>
    /// Parse ngày về UTC. Toàn hệ thống lưu UTC (xem <c>ApplyUtcDateTimeKind</c>), nên một
    /// literal không có múi giờ được hiểu là ĐÃ LÀ UTC — không tự cộng trừ theo giờ máy chủ,
    /// vì máy chủ ở múi nào là chuyện không ai muốn kết quả bộ lọc phụ thuộc vào.
    /// </summary>
    private static DateTime ParseUtcDate(string value, string label)
    {
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
            throw new BusinessRuleException(
                $"Giá trị '{value}' của trường '{label}' không phải một mốc thời gian hợp lệ.");

        return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
    }

    /// <summary>
    /// 🔴 Enum khớp theo <b>TÊN</b>, không theo số. Cả <see cref="Priority"/> lẫn
    /// <see cref="StatusCategory"/> đều lưu dạng int trong DB, và ADR-052 đã trả giá đúng cho
    /// việc coi số của một enum là ổn định: <c>Status</c> và <c>StatusCategory</c> lệch nhau
    /// khiến mọi task <c>Review</c> suýt bị đọc thành <c>Done</c>. Một bộ lọc đã lưu mang số
    /// sẽ âm thầm đổi nghĩa nếu enum thêm thành viên; mang tên thì hoặc đúng, hoặc báo lỗi.
    /// </summary>
    private static int ParseEnumByName(TaskField field, string value, string label)
    {
        switch (field)
        {
            case TaskField.Priority when Enum.TryParse<Priority>(value, ignoreCase: true, out var p)
                                         && Enum.IsDefined(p):
                return (int)p;

            case TaskField.Category when Enum.TryParse<StatusCategory>(value, ignoreCase: true, out var c)
                                         && Enum.IsDefined(c):
                return (int)c;

            default:
                var allowed = field == TaskField.Priority
                    ? string.Join(", ", Enum.GetNames<Priority>())
                    : string.Join(", ", Enum.GetNames<StatusCategory>());

                throw new BusinessRuleException(
                    $"Giá trị '{value}' của trường '{label}' không hợp lệ. Nhận: {allowed}.");
        }
    }
}
