using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// Một lá phiếu trên một yêu cầu duyệt (ADR-062).
///
/// <para>
/// Là bảng riêng chứ không phải hai cột trên <see cref="Approval"/>, vì quorum cần
/// <b>nhiều</b> người ký cùng một yêu cầu, và câu hỏi kiểm toán luôn là "ai ký" chứ không
/// phải "đã ký chưa". Kế thừa <see cref="BaseEntity"/> (khác
/// <see cref="ApprovalPolicyApprover"/>): cặp (Approval, Approver) là duy nhất nhưng hàng
/// này mang dữ liệu riêng — lý do và thời điểm — nên nó là một thực thể, không phải một
/// liên kết.
/// </para>
/// </summary>
public class ApprovalDecision : BaseEntity
{
    public Guid ApprovalId { get; set; }
    public Approval Approval { get; set; } = null!;

    public Guid ApproverId { get; set; }
    public Employee Approver { get; set; } = null!;

    public DecisionKind Decision { get; set; }

    /// <summary>
    /// Lý do. Không bắt buộc kể cả khi từ chối — cưỡng chế một ô chữ không làm lý do trở nên
    /// thật, nó chỉ sinh ra những dòng "n/a". Frontend thì vẫn nên nhắc.
    /// </summary>
    public string? Comment { get; set; }

    public DateTime DecidedAt { get; set; }
}
