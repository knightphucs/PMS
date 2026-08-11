namespace PMS.Domain.Enums;

/// <summary>
/// Kiểu dữ liệu của một trường tuỳ biến (ADR-059).
///
/// <para>
/// 🔴 <b>Danh mục ĐÓNG</b>, cùng lý lẽ với <see cref="StatusCategory"/> (ADR-052) và danh
/// mục quyền hệ thống (ADR-045): người dùng tự đặt <i>tên</i> và <i>ý nghĩa</i> của trường,
/// nhưng <i>hình dạng dữ liệu</i> thì không — mã nguồn phải biết đọc/ghi/validate/hiển thị
/// từng kiểu, nên mỗi kiểu mới là một thay đổi có chủ đích ở cả bốn chỗ đó.
/// </para>
/// <para>
/// Đây chính là ranh giới làm cho "đội tự dựng không gian của họ" khả thi mà không biến
/// hệ thống thành một cái kho JSON không kiểm chứng được gì.
/// </para>
/// </summary>
public enum FieldType
{
    /// <summary>Văn bản một dòng. Lưu ở <c>ValueText</c>.</summary>
    Text,

    /// <summary>Số thập phân. Lưu ở <c>ValueNumber</c> (<c>decimal(18,4)</c>).</summary>
    Number,

    /// <summary>Ngày (không giờ). Lưu ở <c>ValueDate</c>.</summary>
    Date,

    /// <summary>Có/không. Lưu ở <c>ValueBoolean</c>.</summary>
    Checkbox,

    /// <summary>Đường dẫn http(s). Lưu ở <c>ValueText</c>, khác Text ở chỗ validate + render.</summary>
    Url,

    /// <summary>Chọn MỘT trong danh sách <c>FieldOption</c>. Giá trị nằm ở bảng nối.</summary>
    SingleSelect,

    /// <summary>Chọn NHIỀU trong danh sách <c>FieldOption</c>. Giá trị nằm ở bảng nối.</summary>
    MultiSelect,
}
