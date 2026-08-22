namespace PMS.Domain.Enums;

/// <summary>
/// Ai được quyết định trên một <c>ApprovalPolicy</c> (ADR-062). Danh mục ĐÓNG.
///
/// <para>
/// ⚠️ <b>Cả hai chế độ đều KHÔNG đi qua <c>ProjectPermissions</c>.</b> Quyền *quyết định*
/// là quyền theo LUẬT chứ không theo vai trò trong project — đây là ngoại lệ có chủ đích
/// thứ hai của mô hình hai tầng (thứ nhất là <c>Notification</c>, ADR-023). Ghi rõ ở đây
/// và có test riêng, nếu không phiên sau sẽ "sửa" nó về cho nhất quán.
/// </para>
/// </summary>
public enum ApproverMode
{
    /// <summary>
    /// Mọi <c>ProjectManager</c> của project đều duyệt được. Chọn mặc định vì nó không đòi
    /// khai thêm dữ liệu và không bao giờ khoá chết khi một người rời dự án.
    /// </summary>
    ProjectManagers,

    /// <summary>
    /// Chỉ những người có tên trong <c>ApprovalPolicyApprovers</c>.
    ///
    /// <para>
    /// 🔴 Người ngoài danh sách nhận <b>403</b> dù họ là PM — đó chính là điểm của chế độ
    /// này ("chỉ trưởng phòng ký được"). Nếu muốn PM cũng ký được thì dùng chế độ kia, đừng
    /// nới luật ở đây.
    /// </para>
    /// </summary>
    NamedApprovers
}
