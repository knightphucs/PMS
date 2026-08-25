namespace PMS.Domain.Enums;

/// <summary>
/// Trạng thái duyệt của MỘT task, nhìn từ phía người lọc danh sách (ADR-063) — không phải
/// trạng thái của một yêu cầu duyệt (đó là <see cref="ApprovalStatus"/>).
///
/// <para>
/// 🔑 <b>Hai enum này không thay thế nhau được, và đó là chủ đích.</b>
/// <see cref="ApprovalStatus"/> mô tả vòng đời của một <see cref="Entities.Approval"/> —
/// một hàng trong nhật ký, và một task có nhiều hàng như vậy trong đời (ADR-062). Enum này
/// trả lời một câu hỏi khác hẳn: <i>"ngay lúc này, thẻ này có đang vướng cổng duyệt
/// không?"</i> Nó là một phép chiếu của <b>hàng còn hiệu lực</b>
/// (<c>ConsumedAt IS NULL</c>) xuống chính task đó.
/// </para>
/// <para>
/// 🔴 Danh mục ĐÓNG (luật 2 của Doctrine §0). Nó là giá trị của một
/// <see cref="FilterValueKind.Enum"/> trong <see cref="Entities.SavedViewFilter"/>, nên mỗi
/// thành viên phải có một nhánh dịch tường minh sang LINQ ở <c>TaskRepository</c>.
/// </para>
/// <para>
/// 📌 <b>Cố ý KHÔNG có</b> "chờ TÔI duyệt". Một <see cref="Entities.SavedView"/> lưu giá
/// trị <b>literal</b>, không có sentinel "người đang đăng nhập" — nên một view CHIA SẺ mang
/// điều kiện đó sẽ nói dối mọi người trừ tác giả của nó. Xem khoảng trống đã ghi ở cuối
/// ADR-063; lời giải nằm ở một cờ <c>UseCurrentUser</c> trên chính bộ lọc, không ở đây.
/// </para>
/// </summary>
public enum TaskApprovalState
{
    /// <summary>
    /// Không có yêu cầu duyệt nào còn hiệu lực. Đây là trạng thái của <b>đại đa số</b> task
    /// trong hệ thống — kể cả task đã đi qua cổng xong xuôi (hàng cũ đã <c>ConsumedAt</c>).
    /// </summary>
    None,

    /// <summary>Đang chờ chữ ký. Đây là thứ hàng đợi của một hội đồng duyệt lọc theo.</summary>
    Pending,

    /// <summary>
    /// Đã đủ quorum nhưng <b>chưa ai kéo task qua cổng</b>. Một trạng thái ngắn nhưng có
    /// thật, và đáng lọc: nó chính là danh sách "đã được duyệt, đang chờ ai đó thực thi".
    /// </summary>
    Approved,

    /// <summary>
    /// Bị từ chối và <b>vẫn đang chặn</b> — lời từ chối không tự tiêu thụ, phải có người huỷ
    /// tường minh (ADR-062 quyết định c). Vì vậy đây là một hàng đợi cần hành động, không
    /// phải một kết quả đã đóng sổ.
    /// </summary>
    Rejected
}
