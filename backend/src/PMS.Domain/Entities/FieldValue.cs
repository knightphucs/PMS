using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// Giá trị của MỘT trường tuỳ biến trên MỘT task (ADR-059).
///
/// <para>
/// 🔴 <b>Cột có KIỂU, không phải một cột JSON duy nhất.</b> Gói tất cả vào
/// <c>NVARCHAR(MAX)</c> thì viết nhanh hơn nhiều, và hỏng ở đúng chỗ quan trọng: bộ lọc và
/// sắp xếp của "view lưu được" (ADR-061) sẽ so sánh ngày và số dưới dạng CHUỖI — nghĩa là
/// <c>"9"</c> lớn hơn <c>"10"</c>, và không index nào dùng được. Chọn kiểu ở tầng lưu trữ
/// là chọn cho tính năng kế tiếp, không phải cho tính năng này.
/// </para>
/// <para>
/// Đúng MỘT trong bốn cột giá trị được phép khác null (CHECK constraint ở tầng DB), và
/// <i>cột nào</i> thì do <see cref="FieldDefinition.Type"/> quyết định. Ràng buộc thứ hai
/// đó KHÔNG diễn đạt được bằng CHECK vì nó cần đọc bảng khác — nó nằm ở
/// <see cref="Set"/> và ở validator của service.
/// </para>
/// <para>
/// Với Select thì cả bốn cột đều null: giá trị nằm ở bảng nối <see cref="SelectedOptions"/>.
/// </para>
/// </summary>
public class FieldValue : BaseEntity
{
    public Guid TaskId { get; set; }
    public TaskItem Task { get; set; } = null!;

    public Guid FieldDefinitionId { get; set; }
    public FieldDefinition FieldDefinition { get; set; } = null!;

    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }
    public DateTime? ValueDate { get; set; }
    public bool? ValueBoolean { get; set; }

    /// <summary>
    /// Lựa chọn đang chọn. SingleSelect có tối đa MỘT phần tử — bất biến đó do
    /// <see cref="Set"/> giữ, không phải do lược đồ (một bảng nối không diễn đạt được
    /// "tối đa một" mà không thêm cột thừa).
    /// </summary>
    public ICollection<FieldOption> SelectedOptions { get; set; } = new List<FieldOption>();

    /// <summary>
    /// Người ghi DUY NHẤT của bốn cột giá trị. Gán tay từng cột ở service là cách chắc chắn
    /// có lúc quên xoá cột cũ khi đổi giá trị — và khi đó CHECK constraint sẽ từ chối cả
    /// lệnh ghi bằng một lỗi ở tầng DB, xa chỗ gây ra hàng chục dòng.
    /// </summary>
    public void Set(FieldType type, string? text, decimal? number, DateTime? date, bool? boolean)
    {
        ValueText = null;
        ValueNumber = null;
        ValueDate = null;
        ValueBoolean = null;

        switch (type)
        {
            case FieldType.Text:
            case FieldType.Url:
                ValueText = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
                break;
            case FieldType.Number:
                ValueNumber = number;
                break;
            case FieldType.Date:
                // Cắt về nửa đêm UTC: kiểu này là NGÀY, không phải mốc thời gian. Giữ lại
                // phần giờ do client gửi sẽ làm hai người ở hai múi giờ thấy hai ngày khác
                // nhau cho cùng một giá trị — đúng lớp lỗi ADR-046b đã xử lý một lần.
                ValueDate = date is null
                    ? null
                    : DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
                break;
            case FieldType.Checkbox:
                ValueBoolean = boolean;
                break;
            case FieldType.SingleSelect:
            case FieldType.MultiSelect:
                // Giá trị nằm ở SelectedOptions; bốn cột trên phải để trống.
                break;
        }
    }

    /// <summary>Không mang giá trị nào — service dùng để xoá hẳn hàng thay vì giữ rác.</summary>
    public bool IsEmpty =>
        ValueText is null && ValueNumber is null && ValueDate is null
        && ValueBoolean is null && SelectedOptions.Count == 0;
}
