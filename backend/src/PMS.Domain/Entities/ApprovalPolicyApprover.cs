namespace PMS.Domain.Entities;

/// <summary>
/// Một người có tên trong danh sách duyệt của một cổng (ADR-062).
///
/// <para>
/// 🔴 <b>Khoá chính GHÉP</b> <c>(ApprovalPolicyId, EmployeeId)</c>, không kế thừa
/// <see cref="Common.BaseEntity"/> — cùng khuôn <see cref="Watcher"/> (ADR-036) và
/// <see cref="WorkItemTypeField"/> (ADR-060). Cặp đó vốn đã là định danh tự nhiên, và khoá
/// ghép chặn trùng bằng <i>lược đồ</i> thay vì bằng một unique index phải nhớ khai thêm.
/// </para>
/// <para>
/// ⚠️ Đường GHI vào bảng này phải dùng bản đọc <b>CÓ tracking</b>. <c>AsNoTracking</c> trên
/// một quan hệ nhiều-nhiều làm EF coi hàng đã có là hàng mới và sinh INSERT lại →
/// <c>Violation of PRIMARY KEY constraint</c>. Đã trả giá một lần ở ADR-060.
/// </para>
/// </summary>
public class ApprovalPolicyApprover
{
    public Guid ApprovalPolicyId { get; set; }
    public ApprovalPolicy ApprovalPolicy { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
}
