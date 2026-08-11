using PMS.Domain.Common;

namespace PMS.Domain.Entities;

/// <summary>
/// Một lựa chọn của trường kiểu Select (ADR-059).
///
/// <para>
/// 🔴 <b>Bảng riêng, KHÔNG phải một chuỗi "a,b,c" trên <see cref="FieldDefinition"/>.</b>
/// Danh sách nhét trong một cột là dạng phi chuẩn hoá kinh điển, và nó vỡ ngay ở thao tác
/// đầu tiên người dùng sẽ làm: đổi tên một lựa chọn. Với chuỗi thì phải đi sửa chuỗi ở
/// <i>mọi</i> task đang mang giá trị đó — một thao tác không nguyên tử và không có gì bảo
/// đảm nó chạy hết. Với bảng, <see cref="Label"/> đổi ở đúng một hàng và mọi task đang trỏ
/// tới nó tự đúng theo.
/// </para>
/// <para>
/// Đề bài cũng yêu cầu tường minh "xây dựng cơ sở dữ liệu quan hệ để mapping các đối tượng
/// một cách logic" (§1) — nhét danh sách vào một cột văn bản là đi ngược đúng yêu cầu đó.
/// </para>
/// </summary>
public class FieldOption : BaseEntity
{
    public Guid FieldDefinitionId { get; set; }
    public FieldDefinition FieldDefinition { get; set; } = null!;

    public string Label { get; set; } = string.Empty;

    /// <summary>Mã màu <c>#RRGGBB</c> cho chip — cùng khuôn với <see cref="BoardColumn.Color"/>.</summary>
    public string Color { get; set; } = "#6B7280";

    public int Order { get; set; }

    /// <summary>Các giá trị đang chọn lựa chọn này (many-to-many qua bảng nối FieldValueOptions).</summary>
    public ICollection<FieldValue> Values { get; set; } = new List<FieldValue>();
}
