using PMS.Domain.Common;

namespace PMS.Domain.Entities;

/// <summary>
/// Nhãn phân loại task. <b>Toàn cục</b> — dùng chung giữa mọi project, tên duy nhất toàn
/// hệ thống. Hệ quả về quyền (sửa/xóa chỉ SystemAdmin, vì xóa một nhãn là gỡ chip khỏi
/// mọi board của mọi project) và phương án nhãn-theo-project đã hoãn: xem ADR-037.
/// </summary>
public class Label : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Màu chip dạng <c>#RRGGBB</c>, validate bằng regex ở tầng Application.</summary>
    public string Color { get; set; } = DefaultColor;

    public const string DefaultColor = "#6B7280";

    public ICollection<TaskItem> Tasks { get; set; } = [];
}