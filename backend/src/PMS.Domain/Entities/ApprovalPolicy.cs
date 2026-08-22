using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

/// <summary>
/// Một CỔNG duyệt: "task thuộc loại X muốn vào cột Y thì cần N người ký" (ADR-062).
///
/// <para>
/// 🔑 <b>Đây là ĐỘNG TỪ đầu tiên của hệ thống.</b> ADR-052 cho đội tự khai cột, ADR-059 tự
/// khai trường, ADR-060 tự khai loại việc, ADR-061 tự dựng góc nhìn — tất cả đều là DANH TỪ.
/// Khai xong một loại việc tên "Change Request" thì nó vẫn chỉ là một cái thẻ đẹp: không ai
/// phải ký duyệt gì cả, và hệ thống không có cách nào diễn đạt điều đó (§0, "đủ danh từ,
/// thiếu động từ"). Bảng này là chỗ điều đó diễn đạt được.
/// </para>
/// <para>
/// 🔴 <b>Vì sao đây KHÔNG phải khôi phục ma trận chuyển trạng thái mà ADR-052 đã gỡ:</b>
/// ADR-052 gỡ ma trận vì hệ thống <i>đoán hộ</i> người dùng — với cột do người dùng tạo thì
/// mã nguồn không có cơ sở nào để biết "Chờ QA" đứng trước hay sau "Đang sửa". Ở đây
/// <b>người dùng tự khai luật của chính họ</b>. Cùng một cơ chế, ngược chiều quyền sở hữu —
/// và chiều đó là toàn bộ khác biệt.
/// </para>
/// </summary>
public class ApprovalPolicy : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Loại công việc bị cổng này áp lên (ADR-060). Bắt buộc: một cổng áp cho MỌI loại việc
    /// sẽ bắt cả những task hành chính vặt phải đi xin chữ ký.
    /// </summary>
    public Guid WorkItemTypeId { get; set; }
    public WorkItemType WorkItemType { get; set; } = null!;

    /// <summary>
    /// Cột mà task phải được duyệt mới vào được (ADR-052).
    ///
    /// <para>
    /// Gác theo CỘT ĐÍCH chứ không theo cặp (cột nguồn, cột đích): luật nghiệp vụ thật là
    /// "muốn bắt đầu triển khai thì phải có chữ ký", không phụ thuộc thẻ đang nằm đâu. Gác
    /// theo cặp sẽ là ma trận, đúng thứ ADR-052 đã gỡ.
    /// </para>
    /// </summary>
    public Guid TargetColumnId { get; set; }
    public BoardColumn TargetColumn { get; set; } = null!;

    public ApproverMode ApproverMode { get; set; }

    /// <summary>
    /// Số phiếu thuận cần có (quorum). <c>1</c> là ca thường; <c>2</c> là ca CAB điển hình.
    ///
    /// <para>
    /// ⚠️ Với <see cref="Enums.ApproverMode.NamedApprovers"/>, giá trị này không được lớn hơn
    /// số approver đã khai — nếu không thì cổng <b>không bao giờ mở được</b>, và người dùng
    /// sẽ không hiểu vì sao. Cưỡng chế ở validator.
    /// </para>
    /// </summary>
    public int MinApprovals { get; set; } = 1;

    /// <summary>
    /// Thứ tự hiển thị ở màn cấu hình — <b>chỉ vậy thôi</b>.
    ///
    /// <para>
    /// 📌 Cố ý KHÔNG mang nghĩa "chặng thứ mấy": duyệt nhiều chặng tuần tự (Trưởng phòng rồi
    /// Giám đốc) chưa ship, vì nó cần khái niệm "chặng đang chờ" mà chưa có màn nào diễn đạt
    /// được (luật 2 của Doctrine §0 — khái niệm chưa khai được hợp đồng đóng là khái niệm
    /// chưa chín). Unique index trên (ProjectId, WorkItemTypeId, TargetColumnId) đảm bảo mỗi
    /// cổng chỉ có đúng một luật, nên không có thứ tự nào để mà diễn giải.
    /// </para>
    /// </summary>
    public int Order { get; set; }

    /// <summary>Danh sách người duyệt — chỉ dùng khi <see cref="ApproverMode"/> là
    /// <see cref="Enums.ApproverMode.NamedApprovers"/>.</summary>
    public ICollection<ApprovalPolicyApprover> Approvers { get; set; } = new List<ApprovalPolicyApprover>();
}
