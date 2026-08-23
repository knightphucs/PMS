using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// MỘT lần đi qua một cổng duyệt (ADR-062) — không phải "trạng thái duyệt của task".
///
/// <para>
/// 🔑 <b>Sự khác biệt đó là toàn bộ thiết kế của lớp này.</b> Một task có thể đi qua cùng
/// một cổng nhiều lần trong đời (triển khai, bị đá về, triển khai lại), và mỗi lần là một
/// chữ ký riêng. Vì vậy bảng này là <b>nhật ký</b>, không phải một cột trạng thái: hàng cũ
/// ở nguyên đó vĩnh viễn để trả lời "ai đã ký lần triển khai thứ hai" — đúng câu hỏi mà
/// "xuất kiểm toán" (§14, Giai đoạn 3) sẽ hỏi tới.
/// </para>
/// </summary>
public class Approval : BaseEntity
{
    public Guid TaskId { get; set; }
    public TaskItem Task { get; set; } = null!;

    public Guid ApprovalPolicyId { get; set; }
    public ApprovalPolicy ApprovalPolicy { get; set; } = null!;

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    /// <summary>
    /// Người đã kéo task vào cột có cổng, tức người yêu cầu duyệt.
    ///
    /// <para>
    /// 📌 Không có nút "Gửi duyệt" nào cả — hàng này TỰ SINH lúc bị chặn (ADR-062 quyết định
    /// a). Thêm một nút là thêm một bề mặt lên màn làm việc (luật 5 Doctrine), và tệ hơn, nó
    /// bắt người dùng <i>biết trước</i> rằng loại việc này có cổng.
    /// </para>
    /// </summary>
    public Guid RequestedById { get; set; }
    public Employee RequestedBy { get; set; } = null!;

    public DateTime RequestedAt { get; set; }

    /// <summary>Lúc chốt sổ — đủ quorum, bị từ chối, hoặc bị huỷ. Null khi còn
    /// <see cref="ApprovalStatus.Pending"/>.</summary>
    public DateTime? DecidedAt { get; set; }

    /// <summary>
    /// Lúc yêu cầu này được TIÊU THỤ, tức lúc task thật sự đi qua cổng (hoặc lúc bị huỷ).
    ///
    /// <para>
    /// 🔴 <b>"Đã duyệt" và "đã dùng" là hai trục độc lập</b>, nên đây là một cột riêng chứ
    /// không phải một giá trị thứ năm của <see cref="ApprovalStatus"/>. Nhồi vào enum sẽ
    /// khiến một hàng <c>Approved</c> đã dùng rồi không phân biệt được với một hàng
    /// <c>Approved</c> đang chờ dùng — và ta mất luôn vết kiểm toán "ai ký lần nào".
    /// </para>
    /// <para>
    /// Đây cũng là chỗ quyết định "task rời cột đích rồi quay lại thì phải duyệt LẠI" được
    /// cài đặt: guard chỉ nhìn hàng có <c>ConsumedAt IS NULL</c>, nên khi hàng cũ đã tiêu
    /// thụ thì lần quay lại sinh một yêu cầu hoàn toàn mới.
    /// </para>
    /// </summary>
    public DateTime? ConsumedAt { get; set; }

    public ICollection<ApprovalDecision> Decisions { get; set; } = new List<ApprovalDecision>();

    /// <summary>Yêu cầu còn hiệu lực trên cổng — thứ duy nhất guard quan tâm.</summary>
    public bool IsActive => ConsumedAt is null;

    public int ApproveCount => Decisions.Count(d => d.Decision == DecisionKind.Approve);

    /// <summary>
    /// Ghi một lá phiếu và chốt lại trạng thái.
    ///
    /// <para>
    /// Luật đếm: <b>một phiếu chống giết cả yêu cầu ngay</b>, không chờ đủ quorum — đó là
    /// ngữ nghĩa chuẩn của một hội đồng duyệt thay đổi, và cũng là thứ khiến lời từ chối có
    /// nghĩa. Phiếu thuận thì cộng dồn cho tới <paramref name="minApprovals"/>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Chặn trùng theo <paramref name="approverId"/> ở đây <b>và</b> bằng khoá ghép ở DB.
    /// Kiểm ở đây để trả 409 có thông điệp đọc được thay vì để unique index ném
    /// <c>DbUpdateException</c> → 500 — cùng khuôn <c>SavedViewService</c>.
    /// </remarks>
    public ApprovalDecision AddDecision(
        Guid approverId, DecisionKind kind, string? comment, int minApprovals, DateTime now)
    {
        // DomainException -> 409 qua GlobalExceptionHandler (ADR-011).
        if (Status != ApprovalStatus.Pending)
            throw new DomainException("Yêu cầu duyệt này đã được chốt, không nhận thêm quyết định.");

        if (Decisions.Any(d => d.ApproverId == approverId))
            throw new DomainException("Bạn đã quyết định trên yêu cầu duyệt này rồi.");

        var decision = new ApprovalDecision
        {
            Id = Guid.NewGuid(),
            ApprovalId = Id,
            ApproverId = approverId,
            Decision = kind,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            DecidedAt = now,
        };

        Decisions.Add(decision);

        if (kind == DecisionKind.Reject)
        {
            Status = ApprovalStatus.Rejected;
            DecidedAt = now;
        }
        else if (ApproveCount >= minApprovals)
        {
            Status = ApprovalStatus.Approved;
            DecidedAt = now;
        }

        return decision;
    }

    /// <summary>
    /// Đánh dấu đã tiêu thụ — gọi đúng lúc task được phép đi qua cổng.
    /// <b>Không</b> đổi <see cref="Status"/>: hàng ở nguyên <c>Approved</c> làm lịch sử.
    /// </summary>
    public void Consume(DateTime now) => ConsumedAt ??= now;

    /// <summary>
    /// Rút yêu cầu về. Đây là <b>lối thoát duy nhất</b> khỏi một
    /// <see cref="ApprovalStatus.Rejected"/> — cố ý, để việc bỏ qua một lời từ chối là một
    /// hành động có người chịu trách nhiệm chứ không phải một cú kéo chuột (ADR-062 (c)).
    /// </summary>
    public void Cancel(DateTime now)
    {
        if (!IsActive)
            throw new DomainException("Yêu cầu duyệt này đã khép lại, không huỷ được nữa.");

        Status = ApprovalStatus.Cancelled;
        DecidedAt ??= now;
        ConsumedAt = now;
    }
}
