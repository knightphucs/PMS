using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

/// <summary>
/// Phê duyệt (ADR-062). Một repository cho CẢ HAI nhóm bảng — cấu hình
/// (<c>ApprovalPolicy</c>) lẫn dữ liệu chạy (<c>Approval</c>) — vì chúng luôn được đọc cùng
/// nhau trên đường nóng của guard, và tách đôi sẽ đổi lấy hai lần round-trip cho không.
/// <c>IRepository&lt;Approval&gt;</c> phủ đường ghi của nhóm sau; nhóm trước có
/// <see cref="AddPolicy"/>/<see cref="RemovePolicy"/> riêng.
/// </summary>
public interface IApprovalRepository : IRepository<Approval>
{
    // ---------- luật duyệt (cấu hình) ----------

    void AddPolicy(ApprovalPolicy policy);
    void RemovePolicy(ApprovalPolicy policy);

    /// <summary>Mọi cổng duyệt của project, kèm approver, sắp theo <c>Order</c> rồi <c>Id</c>.</summary>
    Task<IReadOnlyList<ApprovalPolicy>> ListPoliciesAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>Một cổng kèm <c>Approvers</c>, <b>CÓ tracking</b> (để sửa/xoá).</summary>
    /// <remarks>
    /// 🔴 Có tracking là bắt buộc chứ không phải tiện tay: đường GHI vào
    /// <c>ApprovalPolicyApprovers</c> là một quan hệ nhiều-nhiều, và <c>AsNoTracking</c> làm
    /// EF coi hàng đã có là hàng mới rồi sinh INSERT lại →
    /// <c>Violation of PRIMARY KEY constraint</c> (bẫy đã trả giá ở ADR-060).
    /// </remarks>
    Task<ApprovalPolicy?> GetPolicyWithApproversAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Cổng áp lên nước đi (loại việc này → cột này), hoặc null nếu không có cổng nào.
    ///
    /// <para>
    /// Đây là truy vấn nằm trên đường nóng: nó chạy ở <b>mọi</b> lần đổi trạng thái của mọi
    /// task trong hệ thống, kể cả ở những project chưa từng khai một luật duyệt nào. Vì vậy
    /// nó phải đi thẳng vào unique index <c>(ProjectId, WorkItemTypeId, TargetColumnId)</c>
    /// và không <c>Include</c> gì ngoài <c>Approvers</c>.
    /// </para>
    /// </summary>
    Task<ApprovalPolicy?> FindGateAsync(
        Guid projectId, Guid workItemTypeId, Guid targetColumnId, CancellationToken ct = default);

    /// <summary>Có cổng nào áp lên loại việc này không — nuôi việc TỰ ẨN khối duyệt ở UI.</summary>
    Task<bool> HasAnyGateForTypeAsync(
        Guid projectId, Guid workItemTypeId, CancellationToken ct = default);

    // ---------- yêu cầu duyệt (dữ liệu chạy) ----------

    /// <summary>
    /// Yêu cầu còn HIỆU LỰC của một task trên một cổng (<c>ConsumedAt IS NULL</c>), kèm
    /// <c>Decisions</c> và người quyết định. <b>CÓ tracking</b> — đây là đường ghi.
    /// </summary>
    /// <remarks>
    /// Bất biến "mỗi cặp (task, cổng) có tối đa MỘT hàng hiệu lực" do
    /// <c>EnsureApprovedAsync</c> giữ: nhánh <c>Pending</c> không sinh hàng mới. Ở đây dùng
    /// <c>OrderByDescending(RequestedAt)</c> làm chốt chặn để một hàng lạc không âm thầm
    /// khiến guard đọc phải yêu cầu cũ.
    /// </remarks>
    Task<Approval?> GetActiveAsync(
        Guid taskId, Guid policyId, CancellationToken ct = default);

    /// <summary>Toàn bộ lịch sử duyệt của một task — hàng hiệu lực lẫn hàng đã tiêu thụ.</summary>
    Task<IReadOnlyList<Approval>> ListByTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Một yêu cầu kèm <c>Decisions</c> + cổng + approver, <b>CÓ tracking</b>.</summary>
    Task<Approval?> GetWithDecisionsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Xoá mọi cổng duyệt trỏ tới một loại việc hoặc một cột — gọi TRƯỚC khi xoá thứ đó.
    ///
    /// <para>
    /// 🔴 Tồn tại vì <c>ApprovalPolicies</c> treo dưới <c>WorkItemTypes</c>/<c>BoardColumns</c>
    /// bằng <c>Restrict</c> (xem sơ đồ cascade ở <c>ApprovalConfigurations</c>): không dọn
    /// trước thì <c>DELETE</c> ném <c>DbUpdateException</c> → <b>500</b>, ở một đường xoá mà
    /// bộ test đã đi qua sẵn nên trông như một lỗi có sẵn.
    /// </para>
    /// </summary>
    Task<int> DeletePoliciesForAsync(
        Guid? workItemTypeId, Guid? boardColumnId, CancellationToken ct = default);

    /// <summary>
    /// Xoá mọi yêu cầu duyệt thuộc một cổng — gọi TRƯỚC khi xoá chính cổng đó.
    ///
    /// <para>
    /// Cùng lý do <see cref="DeletePoliciesForAsync"/>: <c>Approvals</c> treo dưới
    /// <c>ApprovalPolicies</c> bằng <c>Restrict</c>. <c>ApprovalDecisions</c> đi theo bằng
    /// <c>Cascade</c> nên không phải dọn tay.
    /// </para>
    /// </summary>
    Task<int> DeleteApprovalsForPolicyAsync(Guid policyId, CancellationToken ct = default);
}
