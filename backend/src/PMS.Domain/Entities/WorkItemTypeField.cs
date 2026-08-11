namespace PMS.Domain.Entities;

/// <summary>
/// Một trường tuỳ biến được gắn vào một loại công việc (ADR-060).
///
/// <para>
/// 🔴 <b>Khoá chính GHÉP</b> <c>(WorkItemTypeId, FieldDefinitionId)</c>, không kế thừa
/// <see cref="Common.BaseEntity"/> — cùng khuôn <see cref="Watcher"/> (ADR-036). Cặp đó
/// vốn đã là định danh tự nhiên, và khoá ghép chặn trùng bằng <i>lược đồ</i> thay vì bằng
/// một unique index phải nhớ khai thêm.
/// </para>
/// <para>
/// Đây cũng là chỗ <see cref="IsRequired"/> cuối cùng có điểm cưỡng chế thật. ADR-059 cố ý
/// KHÔNG ship cờ này trên <see cref="FieldDefinition"/>: ở đó nó là một cờ toàn cục cho cả
/// project, và bật lên sẽ làm mọi task đang có trở thành không hợp lệ. Gắn vào từng LOẠI
/// thì phạm vi hẹp lại đúng mức có nghĩa — "Change Request bắt buộc có Hệ thống ảnh hưởng",
/// chứ không phải "mọi task trong project đều phải có".
/// </para>
/// </summary>
public class WorkItemTypeField
{
    public Guid WorkItemTypeId { get; set; }
    public WorkItemType WorkItemType { get; set; } = null!;

    public Guid FieldDefinitionId { get; set; }
    public FieldDefinition FieldDefinition { get; set; } = null!;

    /// <summary>
    /// Bắt buộc điền với task thuộc loại này.
    ///
    /// <para>
    /// ⚠️ Cưỡng chế ở <b>đường ghi giá trị</b>, KHÔNG áp ngược lên task đã có. Bật cờ này
    /// cho một trường không được biến hàng trăm task cũ thành không hợp lệ — người dùng sẽ
    /// không sửa được bất kỳ trường nào khác cho tới khi điền xong thứ họ không biết là
    /// đang thiếu. Chi tiết ở ADR-060.
    /// </para>
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>Thứ tự hiển thị TRONG loại này — khác <see cref="FieldDefinition.Order"/>
    /// vốn là thứ tự trong toàn project.</summary>
    public int Order { get; set; }
}
