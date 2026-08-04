namespace PMS.Domain.Entities;

/// <summary>
/// Bộ đếm số thứ tự task của một project — mỗi project đúng một hàng (ADR-033).
/// <para>
/// 🔴 <b>KHÔNG kế thừa <c>BaseEntity</c></b>, tiền lệ <see cref="Watcher"/>. Ba hệ quả phải nhớ:
/// khóa chính là <c>ProjectId</c> chứ không phải <c>Id</c>;
/// <c>ApplyIdNeverGenerated()</c> không đụng tới nó;
/// và <c>ApplyAuditFields()</c> duyệt <c>ChangeTracker.Entries&lt;BaseEntity&gt;()</c> nên
/// hàng này <b>không</b> có <c>CreatedAt</c>/<c>UpdatedAt</c> tự động — cố ý, vì nó là hạ
/// tầng đánh số chứ không phải dữ liệu nghiệp vụ cần audit.
/// </para>
/// <para>
/// Chính vì hàng này không có cột audit lẫn cờ soft-delete mà việc cấp số được phép đi
/// bằng một câu <c>UPDATE … OUTPUT</c> thô: không interceptor nào của <c>PmsDbContext</c>
/// bị bỏ qua, nên đây <b>không</b> phải vi phạm lệnh cấm bulk-update của ADR-024.
/// </para>
/// <para>
/// Vì sao tách bảng riêng thay vì để một cột <c>TaskCounter</c> trên <c>Projects</c>:
/// <c>Projects.RowVersion</c> đang được client round-trip theo ADR-016. Cột
/// <c>rowversion</c> của SQL Server đổi khi <b>bất kỳ</b> cột nào của hàng đó đổi, nên mỗi
/// lần tạo task sẽ vô hiệu hóa token mà form sửa project đang giữ → PM nhận 409 giả trên
/// một trường hoàn toàn không liên quan.
/// </para>
/// </summary>
public class ProjectTaskCounter
{
    public Guid ProjectId { get; set; }

    /// <summary>Số ĐÃ cấp gần nhất. Task tiếp theo nhận <c>NextNumber + 1</c>.</summary>
    public int NextNumber { get; set; }

    public Project Project { get; set; } = null!;
}
