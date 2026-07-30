namespace PMS.Application.Common.Authorization;

/// <summary>
/// Mọi hành động cần kiểm quyền tầng 2 (RoleInProject). Task/Sprint dùng chung enum này
/// thay vì có service phân quyền riêng: quyền trên task về bản chất là quyền project-scoped,
/// lấy từ cùng một bảng ProjectMember (ADR-019). Chỉ luật cần dữ liệu per-task — như
/// "người gọi có phải Assignee không" (ADR-017) — mới nằm trong service của Task.
/// </summary>
public enum ProjectAction
{
    View,
    Update,
    Delete,
    ManageMembers,
    ViewStatistics,

    CreateTask,
    UpdateTask,
    DeleteTask,
    ManageAssignees,
    ManageSprint,

    /// <summary>Tự nhận / tự rút khỏi task — Member cũng làm được, Viewer thì không.</summary>
    SelfAssign,

    /// <summary>
    /// Viết comment trên task — §10 cho Member quyền này, Viewer chỉ đọc. Sửa/xóa KHÔNG có
    /// action riêng: chúng phụ thuộc dữ liệu per-row (ai là tác giả) nên nằm trong
    /// CommentService, đúng "ranh giới còn lại" của ADR-019.
    /// </summary>
    CreateComment
}