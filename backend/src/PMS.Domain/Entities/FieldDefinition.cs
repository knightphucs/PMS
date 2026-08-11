using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// Một trường tuỳ biến do đội tự khai cho MỘT project (ADR-059) — hậu bản trực tiếp của
/// ADR-052, nơi cột board đã thôi là enum và trở thành dữ liệu của từng project.
///
/// <para>
/// Đây là thứ biến hệ thống từ "một khuôn cố định" thành "đội tự dựng không gian của họ":
/// phòng hạ tầng khai "Hệ thống ảnh hưởng", "Cửa sổ bảo trì", "Mức rủi ro" mà không cần
/// ai sửa một dòng code nào.
/// </para>
/// <para>
/// 🔴 <b>Cố ý KHÔNG có <c>Key</c>/slug.</b> Định danh là <see cref="BaseEntity.Id"/>; ô
/// người dùng nhìn thấy là <see cref="Label"/> và đổi tên thoải mái. Một slug sẽ kéo theo
/// ràng buộc unique, luật sinh slug, và câu hỏi "đổi tên thì slug có đổi không" — mà mọi
/// câu trả lời đều làm hỏng thứ đang trỏ tới nó (bộ lọc đã lưu ở ADR-061). Guid không có
/// vấn đề đó.
/// </para>
/// </summary>
public class FieldDefinition : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>Tên hiển thị do người dùng đặt — "Hệ thống ảnh hưởng", "Mức rủi ro"…</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Không đổi được sau khi tạo — xem <see cref="FieldValue"/>. Đổi kiểu nghĩa là mọi giá
    /// trị đã nhập nằm sai cột, và không có phép chuyển nào đúng cho mọi dữ liệu.
    /// </summary>
    public FieldType Type { get; set; }

    /// <summary>Thứ tự hiển thị trong khối "Trường tuỳ biến" ở chi tiết task.</summary>
    public int Order { get; set; }

    /// <summary>Chỉ có nghĩa với <see cref="FieldType.SingleSelect"/>/<see cref="FieldType.MultiSelect"/>.</summary>
    public ICollection<FieldOption> Options { get; set; } = new List<FieldOption>();

    public ICollection<FieldValue> Values { get; set; } = new List<FieldValue>();

    public bool IsSelect => Type is FieldType.SingleSelect or FieldType.MultiSelect;
}
