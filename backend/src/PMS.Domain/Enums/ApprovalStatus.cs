namespace PMS.Domain.Enums;

/// <summary>
/// Vòng đời của MỘT yêu cầu phê duyệt (ADR-062).
///
/// <para>
/// 🔴 Đây là danh mục ĐÓNG (luật 2 của Doctrine §0): người dùng đặt tên cho luật duyệt và
/// chọn ai duyệt, nhưng <b>hình dạng của một lần duyệt thì không</b> — mã nguồn phải biết
/// dịch từng trạng thái thành một nhánh của guard.
/// </para>
/// <para>
/// ⚠️ Trạng thái này KHÔNG trả lời câu "yêu cầu này còn hiệu lực không". Đó là việc của
/// <c>Approval.ConsumedAt</c> — "đã duyệt" và "đã dùng" là hai trục độc lập. Xem ADR-062
/// quyết định (b).
/// </para>
/// </summary>
public enum ApprovalStatus
{
    /// <summary>Đã gửi, chưa đủ số người duyệt và chưa ai từ chối.</summary>
    Pending,

    /// <summary>Đủ <c>MinApprovals</c> phiếu thuận. Chỉ khi ở trạng thái này cổng mới mở.</summary>
    Approved,

    /// <summary>
    /// Có ít nhất MỘT phiếu chống. Một phiếu chống giết cả yêu cầu ngay, không cần chờ đủ
    /// quorum — đó là ngữ nghĩa chuẩn của một hội đồng duyệt thay đổi.
    ///
    /// <para>
    /// 🔴 Hàng bị từ chối <b>vẫn chặn</b> (<c>ConsumedAt</c> còn null) cho tới khi có người
    /// huỷ tường minh. Để nó tự tiêu thụ ngay là biến lời từ chối thành thứ chặn được đúng
    /// một cú kéo chuột — xem ADR-062 quyết định (c).
    /// </para>
    /// </summary>
    Rejected,

    /// <summary>
    /// Người gửi hoặc PM rút yêu cầu về. Đây là <b>lối thoát duy nhất</b> khỏi một
    /// <see cref="Rejected"/>, và nó cố ý là một hành động có người chịu trách nhiệm.
    /// </summary>
    Cancelled
}
