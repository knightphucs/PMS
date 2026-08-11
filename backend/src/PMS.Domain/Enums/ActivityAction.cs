namespace PMS.Domain.Enums;

public enum ActivityAction
{
    Created,
    Updated,
    Deleted,
    StatusChanged,
    MemberInvited,
    MemberJoined,
    MemberDeclined,
    MemberRoleChanged,
    MemberRemoved,
    Assigned,
    Unassigned,
    Commented,
    CommentUpdated,
    CommentDeleted,
    AccountLocked,
    AccountUnlocked,
    SystemRoleChanged,

    /// <summary>Đổi tập quyền của một vai trò hệ thống (ADR-045).</summary>
    PermissionsChanged,

    // ---------- Nhóm xác thực (ADR-058, 2026-08-11) ----------
    //
    // Cột Action lưu dạng CHUỖI (HasConversion<string>, tối đa 50 ký tự) nên vị trí trong
    // enum không mang ý nghĩa lưu trữ — thêm ở đâu cũng an toàn, miễn tên đủ ngắn.
    //
    // 🔴 Cả nhóm này dùng EntityType = "Employee", tức chúng TỰ ĐỘNG hiện ở
    // /admin/audit-logs (Employee vốn đã nằm trong SystemScopedEntityTypes). Đó là chủ ý:
    // với một hệ thống nội bộ ngân hàng, đăng nhập và đổi mật khẩu là những dòng kiểm toán
    // nội bộ hỏi tới đầu tiên.

    Registered,
    LoggedIn,

    /// <summary>
    /// Sai mật khẩu HOẶC đăng nhập vào tài khoản đang bị khóa.
    /// <para>
    /// ⚠️ Chỉ ghi được khi email TỒN TẠI — không có <c>Employee</c> thì không có
    /// <c>EmployeeId</c> để quy trách nhiệm, mà <c>ActivityLog.EmployeeId</c> là khóa ngoại
    /// bắt buộc. Thử đăng nhập bằng email không tồn tại vì vậy chỉ nằm ở Serilog. Bịa một
    /// employee "vô danh" để lấp chỗ trống sẽ làm hỏng chính bảng đang dùng để quy trách
    /// nhiệm — xem ADR-058.
    /// </para>
    /// </summary>
    LoginFailed,

    LoggedOut,
    PasswordChanged,
    PasswordResetRequested,
    PasswordResetCompleted,
    ProfileUpdated
}