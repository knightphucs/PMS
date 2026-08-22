using PMS.Domain.Enums;

namespace PMS.Application.Features.Approvals;

// ---------- luật duyệt (cấu hình) ----------

/// <summary>Một người có tên trong danh sách duyệt.</summary>
public record ApproverDto(Guid EmployeeId, string Name);

public record ApprovalPolicyResponse(
    Guid Id,
    Guid ProjectId,
    Guid WorkItemTypeId,
    string WorkItemTypeName,
    Guid TargetColumnId,
    string TargetColumnName,
    ApproverMode ApproverMode,
    int MinApprovals,
    int Order,
    IReadOnlyList<ApproverDto> Approvers);

/// <summary>
/// <paramref name="ApproverIds"/> chỉ có nghĩa khi <paramref name="ApproverMode"/> là
/// <see cref="ApproverMode.NamedApprovers"/> — với chế độ kia nó bị bỏ qua chứ không phải là
/// lỗi 400, vì đổi chế độ trên một form đã điền là chuyện thường và bắt người dùng xoá tay
/// danh sách cũ trước khi lưu là phiền vô cớ.
/// </summary>
public record CreateApprovalPolicyRequest(
    Guid WorkItemTypeId,
    Guid TargetColumnId,
    ApproverMode ApproverMode,
    int MinApprovals,
    IReadOnlyList<Guid>? ApproverIds = null);

/// <summary>Ghi đè TOÀN PHẦN, cùng ngữ nghĩa <c>UpdateSavedViewRequest</c> (ADR-061).</summary>
public record UpdateApprovalPolicyRequest(
    Guid WorkItemTypeId,
    Guid TargetColumnId,
    ApproverMode ApproverMode,
    int MinApprovals,
    IReadOnlyList<Guid>? ApproverIds = null);

// ---------- yêu cầu duyệt (dữ liệu chạy) ----------

public record ApprovalDecisionDto(
    Guid Id,
    Guid ApproverId,
    string ApproverName,
    DecisionKind Decision,
    string? Comment,
    DateTime DecidedAt);

public record ApprovalResponse(
    Guid Id,
    Guid TaskId,
    Guid ApprovalPolicyId,
    string TargetColumnName,
    ApprovalStatus Status,
    Guid RequestedById,
    string RequestedByName,
    DateTime RequestedAt,
    DateTime? DecidedAt,

    /// <summary>Null = yêu cầu này còn hiệu lực và đang chặn cổng. Có giá trị = đã dùng xong
    /// hoặc đã huỷ, hàng chỉ còn là lịch sử.</summary>
    DateTime? ConsumedAt,

    int ApproveCount,
    int MinApprovals,
    IReadOnlyList<ApprovalDecisionDto> Decisions,

    /// <summary>
    /// Người đang gọi có được QUYẾT ĐỊNH trên yêu cầu này không.
    ///
    /// <para>
    /// Backend trả lời thay vì để frontend tự suy, cùng lý lẽ <c>SavedViewResponse.CanEdit</c>
    /// (ADR-061): luật ở đây là <c>ApproverMode</c> + danh sách approver + "chưa bỏ phiếu" +
    /// "yêu cầu còn Pending" — bốn vế, mà frontend chỉ nhìn thấy hai. Hai nơi cùng dựng một
    /// luật thì chắc chắn có lúc lệch (ADR-034).
    /// </para>
    /// </summary>
    bool CanDecide,

    /// <summary>Người đang gọi có huỷ được yêu cầu này không (người gửi HOẶC PM).</summary>
    bool CanCancel);

/// <summary>
/// Trạng thái duyệt của MỘT task — thứ nuôi khối duyệt ở màn chi tiết.
/// </summary>
/// <remarks>
/// <paramref name="HasGate"/> tồn tại riêng để frontend biết <b>TỰ ẨN</b> khối duyệt (luật 3
/// của Doctrine §0). Không có nó thì một task chưa từng đi qua cổng nào và một task thuộc
/// loại không có cổng trông giống hệt nhau — cả hai đều "danh sách rỗng" — và khối sẽ hiện
/// tiêu đề trống trên mọi task của mọi project chưa dùng tính năng.
/// </remarks>
public record TaskApprovalsResponse(
    bool HasGate,
    ApprovalResponse? Active,
    IReadOnlyList<ApprovalResponse> History);

public record CreateApprovalDecisionRequest(DecisionKind Decision, string? Comment = null);
