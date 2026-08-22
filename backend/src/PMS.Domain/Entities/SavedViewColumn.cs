using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// Một cột hiển thị của <see cref="SavedView"/> (ADR-061), theo thứ tự trái→phải.
///
/// <para>
/// Cùng lý lẽ quan hệ với <see cref="SavedViewFilter"/>, và cùng một lợi ích cụ thể: xoá một
/// trường tuỳ biến thì cột tương ứng biến mất khỏi mọi view <b>bằng cascade của DB</b>, không
/// cần một lượt dọn nào ở tầng ứng dụng. Lưu dạng danh sách khoá trong JSON sẽ để lại một cột
/// tiêu đề trống trên mọi view từng dùng tới nó.
/// </para>
/// </summary>
public class SavedViewColumn : BaseEntity
{
    public Guid SavedViewId { get; set; }
    public SavedView SavedView { get; set; } = null!;

    /// <summary>Trường DỰNG SẴN — loại trừ lẫn nhau với <see cref="FieldDefinitionId"/>.</summary>
    public TaskField? Field { get; set; }

    /// <summary>Trường TUỲ BIẾN (ADR-059).</summary>
    public Guid? FieldDefinitionId { get; set; }
    public FieldDefinition? FieldDefinition { get; set; }

    public int Order { get; set; }
}
