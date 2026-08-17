namespace PMS.Domain.Enums;

/// <summary>
/// Toán tử của một dòng bộ lọc trong <see cref="Entities.SavedView"/> (ADR-061).
///
/// <para>
/// 🔴 <b>Danh mục ĐÓNG</b>, cùng lý lẽ <see cref="StatusCategory"/> (ADR-052),
/// <see cref="FieldType"/> (ADR-059) và <c>SystemPermissions</c> (ADR-045): người dùng chọn
/// <i>trường nào</i> và <i>giá trị gì</i>, nhưng <i>phép so sánh</i> thì không — mã nguồn
/// phải biết dịch từng toán tử thành SQL cho từng kiểu dữ liệu. Một toán tử tự do sẽ là một
/// đường cho phép người dùng viết vị từ, tức là một bề mặt injection.
/// </para>
/// <para>
/// Toán tử nào hợp lệ với trường nào <b>không</b> quyết định ở đây mà ở
/// <see cref="FilterValueKind"/>: mỗi trường (dựng sẵn hoặc tuỳ biến) khai mình thuộc một
/// <i>kiểu giá trị</i>, và mỗi kiểu cho phép một tập toán tử. Nhờ vậy thêm một trường dựng
/// sẵn mới không phải sửa một ma trận trường × toán tử.
/// </para>
/// </summary>
public enum FilterOperator
{
    Equals,
    NotEquals,

    /// <summary>Chỉ cho kiểu <see cref="FilterValueKind.Text"/>.</summary>
    Contains,

    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,

    /// <summary>Không có giá trị — không cần <c>Value</c>.</summary>
    IsEmpty,

    /// <summary>Có bất kỳ giá trị nào — không cần <c>Value</c>.</summary>
    IsNotEmpty
}
