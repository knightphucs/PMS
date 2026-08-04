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
    CreateComment,

    /// <summary>
    /// Tải file đính kèm lên Task/Project (ADR-035). Soi gương <see cref="CreateComment"/>:
    /// PM/Member ghi được, Viewer chỉ đọc. XÓA không có action riêng — nó là luật per-row
    /// (người tải lên HOẶC PM), nằm trong AttachmentService đúng khuôn ADR-026.
    /// </summary>
    UploadAttachment,

    /// <summary>
    /// Đăng ký/hủy theo dõi task (ADR-036). Là action RIÊNG chứ không dùng lại
    /// <see cref="View"/>, dù cả ba vai trò đều được: <see cref="View"/> không bao giờ
    /// được phép cho qua một thao tác GHI, kể cả thao tác chỉ ghi cho chính mình.
    /// </summary>
    Watch,

    /// <summary>Gắn/gỡ nhãn trên task — phạm vi project, không ảnh hưởng chéo (ADR-037).</summary>
    ManageTaskLabels,

    /// <summary>Tạo/xóa liên kết giữa hai task (ADR-038) — người làm việc mới biết phụ thuộc.</summary>
    ManageTaskLinks
}