namespace PMS.Domain.Enums;

/// <summary>
/// Nhóm ngữ nghĩa của một cột board (ADR-052).
///
/// <para>
/// 🔴 <b>Đây là thứ giữ cho cột tuỳ biến không phá vỡ phần còn lại của hệ thống.</b> Khi
/// <c>Status</c> còn là enum đóng, cả solution hỏi "task xong chưa?" bằng
/// <c>Status == Status.Done</c> — 39 chỗ, trong đó có guard chặn task đang bị
/// <c>Blocks</c>, phép tính <c>IsOverdue</c>, tiến độ subtask, mọi con số thống kê, và
/// guard "không xóa project còn task chưa xong".
/// </para>
/// <para>
/// Nếu cột chỉ có tên do người dùng đặt thì không câu hỏi nào ở trên trả lời được: một cột
/// tên "Đã ship" hay "Hủy bỏ" thì hệ thống không có cách nào biết nó nghĩa là đã kết thúc.
/// Category là hợp đồng tối thiểu giữa <b>tên do người dùng đặt</b> và <b>ngữ nghĩa mà mã
/// nguồn cần</b>. Jira giải đúng bằng cách này (To Do / In Progress / Done).
/// </para>
/// <para>
/// ⚠️ Cố ý chỉ có <b>ba</b> giá trị và là danh mục <b>ĐÓNG</b>. Người dùng thêm bao nhiêu
/// cột cũng được, nhưng mỗi cột phải khai mình thuộc nhóm nào — nếu cho tự đặt luôn cả
/// nhóm thì lại quay về đúng bài toán ban đầu.
/// </para>
/// </summary>
public enum StatusCategory
{
    /// <summary>Chưa bắt đầu. Task ở đây mới được phép <i>tự nhận việc</i>.</summary>
    ToDo,

    /// <summary>Đang làm. Chuyển vào đây bị chặn nếu task còn blocker chưa xong.</summary>
    InProgress,

    /// <summary>
    /// Đã kết thúc. Mọi phép kiểm "xong chưa" trong solution đọc nhóm này, KHÔNG đọc tên cột.
    /// </summary>
    Done,
}
