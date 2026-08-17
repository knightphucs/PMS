using PMS.Domain.Enums;

namespace PMS.Application.Common.Filtering;

/// <summary>
/// Một điều kiện đã được <b>phân giải</b>: literal chuỗi của người dùng đã parse xong sang
/// đúng kiểu CLR, và toán tử đã được kiểm là hợp lệ với kiểu đó (ADR-061).
///
/// <para>
/// 🔑 Tồn tại để tầng truy vấn <b>không phải parse gì cả</b>. Repository chỉ việc dịch sang
/// biểu thức LINQ. Mọi phép kiểm và mọi thông điệp lỗi đọc được nằm ở
/// <see cref="TaskFilterCatalog"/>, chạy lúc <b>GHI</b> view — một view đã lưu mà tới lúc mở
/// mới báo lỗi là thứ người dùng không sửa được.
/// </para>
/// <para>
/// Mỗi <see cref="FilterValueKind"/> dùng đúng MỘT ô giá trị bên dưới; các ô còn lại là
/// <c>null</c>. Tách ô thay vì một <c>object?</c> chung để tầng truy vấn không phải ép kiểu
/// — ép kiểu sai ở đó sẽ là một <c>InvalidCastException</c> lúc chạy, đúng lớp lỗi mà cột
/// có kiểu của ADR-059 sinh ra để tránh.
/// </para>
/// </summary>
public sealed record ResolvedFilter(
    TaskField? Field,
    Guid? FieldDefinitionId,
    FilterValueKind Kind,
    FilterOperator Operator,
    string? Text = null,
    decimal? Number = null,
    DateTime? Date = null,
    bool? Boolean = null,
    Guid? Reference = null,
    int? EnumValue = null)
{
    /// <summary>Điều kiện này nhắm vào một trường tuỳ biến (ADR-059) chứ không phải trường dựng sẵn.</summary>
    public bool IsCustomField => FieldDefinitionId.HasValue;
}

/// <summary>
/// Toàn bộ đầu vào của một lượt chạy view (ADR-061) — điều kiện, sắp xếp.
/// <para>
/// Phân trang KHÔNG nằm ở đây: nó là <c>PagedRequest</c> có sẵn, và một view không sở hữu
/// số trang của người đang xem.
/// </para>
/// </summary>
public sealed record TaskQuerySpec(
    IReadOnlyList<ResolvedFilter> Filters,
    TaskField? SortBy,
    bool SortDescending)
{
    public static TaskQuerySpec Empty { get; } = new([], null, false);
}
