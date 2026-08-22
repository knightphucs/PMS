using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// Một dòng điều kiện của <see cref="SavedView"/> (ADR-061). Nhiều dòng nối với nhau bằng
/// <b>AND</b>.
///
/// <para>
/// 🔴 <b>Vì sao là BẢNG QUAN HỆ chứ không phải một cột JSON trên <see cref="SavedView"/>.</b>
/// JSON viết nhanh hơn hẳn và hỏng ở đúng chỗ ADR-059 đã trả giá với <c>FieldOption</c>: khi
/// một trường tuỳ biến bị xoá, FK ở đây <b>cascade dọn theo</b>, còn JSON để lại một điều
/// kiện trỏ vào một trường không còn tồn tại — im lặng, không lệnh nào tìm ra, và view thì
/// trả về kết quả sai mà vẫn chạy. Đề bài cũng yêu cầu tường minh "cơ sở dữ liệu quan hệ để
/// mapping các đối tượng một cách logic" (§1).
/// </para>
/// <para>
/// ⚠️ <b>Chỉ hỗ trợ AND.</b> Ghi ra thay vì để người dùng tự phát hiện: chưa biểu diễn được
/// <i>"cột = A HOẶC cột = B"</i>. Thêm OR cần một cây điều kiện có nhóm lồng nhau — đắt hơn
/// nhiều và chưa có nhu cầu thật. Hàng đợi của tầng quy trình (§0) chỉ cần AND.
/// </para>
/// </summary>
public class SavedViewFilter : BaseEntity
{
    public Guid SavedViewId { get; set; }
    public SavedView SavedView { get; set; } = null!;

    /// <summary>
    /// Trường DỰNG SẴN. Loại trừ lẫn nhau với <see cref="FieldDefinitionId"/> — đúng một
    /// trong hai phải có giá trị, có CHECK constraint giữ (cùng khuôn
    /// <see cref="Attachment"/>: hai FK nullable + CHECK đúng-một-chủ).
    /// </summary>
    public TaskField? Field { get; set; }

    /// <summary>Trường TUỲ BIẾN (ADR-059).</summary>
    public Guid? FieldDefinitionId { get; set; }
    public FieldDefinition? FieldDefinition { get; set; }

    public FilterOperator Operator { get; set; }

    /// <summary>
    /// Literal người dùng nhập, lưu dạng chuỗi. <c>null</c> với
    /// <see cref="FilterOperator.IsEmpty"/>/<see cref="FilterOperator.IsNotEmpty"/>.
    ///
    /// <para>
    /// 📌 Lưu chuỗi ở đây <b>không</b> mâu thuẫn với quyết định "cột có kiểu" của ADR-059.
    /// Đây là một <i>literal</i> chờ được phân giải, không phải một <i>giá trị đang so sánh</i>:
    /// nó được parse sang đúng kiểu CLR (<c>decimal</c>/<c>DateTime</c>/<c>Guid</c>/enum) lúc
    /// dựng truy vấn, rồi mới đem so với cột có kiểu. Việc parse xảy ra lúc <b>GHI</b> view
    /// (fail nhanh, 400 kèm thông điệp đọc được) chứ không phải lúc chạy view — một view đã
    /// lưu mà tới lúc mở mới báo lỗi là thứ người dùng không sửa được.
    /// </para>
    /// </summary>
    public string? Value { get; set; }
}
