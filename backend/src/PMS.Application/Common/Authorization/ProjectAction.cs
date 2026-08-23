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
    /// <summary>Member chỉ tạo subtask của task đang được giao cho chính mình.</summary>
    CreateSubtask,
    UpdateTask,
    DeleteTask,
    ManageAssignees,
    ManageSprint,

    /// <summary>
    /// Thêm/sửa/xóa/sắp xếp cột board (ADR-052). Action RIÊNG chứ không mượn
    /// <see cref="Update"/>: đổi cấu hình board tác động tới cách CẢ ĐỘI nhìn công việc,
    /// và nếu sau này muốn nới cho Member thì chỉ cần sửa một dòng ở ProjectPermissions
    /// thay vì phải tách một action đang gánh hai nghĩa.
    /// </summary>
    ManageBoardColumns,

    /// <summary>
    /// Thêm/sửa/xoá/sắp xếp TRƯỜNG TUỲ BIẾN của project (ADR-059). Soi gương
    /// <see cref="ManageBoardColumns"/>: đây là đổi LƯỢC ĐỒ mà cả đội nhìn thấy, khác hẳn
    /// việc NHẬP giá trị vào một task (đi cùng <see cref="UpdateTask"/>). Gộp hai thứ vào
    /// một quyền sẽ hoặc cấm Member nhập liệu, hoặc cho Member đổi lược đồ.
    /// </summary>
    ManageFieldDefinitions,

    /// <summary>
    /// Thêm/sửa/xoá/sắp xếp LOẠI CÔNG VIỆC và gán trường cho loại (ADR-060). Cùng mức với
    /// <see cref="ManageFieldDefinitions"/> và <see cref="ManageBoardColumns"/>: đây là
    /// lược đồ mà cả đội nhìn thấy, không phải dữ liệu của một task.
    /// </summary>
    ManageWorkItemTypes,

    /// <summary>
    /// Thêm/sửa/xoá/sắp xếp view CHIA SẺ của project (ADR-061).
    ///
    /// <para>
    /// ⚠️ Khác ba action lược đồ ở trên ở một điểm quan trọng: <b>view RIÊNG không đi qua
    /// đây</b>. Ai cũng tạo được view của riêng mình (kể cả <c>Viewer</c> — nó chỉ đổi cách
    /// chính họ nhìn danh sách, không ai khác thấy, cùng lý lẽ <see cref="Watch"/> ở ADR-036).
    /// Action này chỉ gác thao tác ghi lên view mà CẢ ĐỘI nhìn thấy, và nó tồn tại song song
    /// với luật "chủ sở hữu tự quản view của mình" nằm trong <c>SavedViewService</c> —
    /// đúng "ranh giới còn lại" của ADR-019: luật cần dữ liệu per-row thì ở service.
    /// </para>
    /// </summary>
    ManageSavedViews,

    /// <summary>
    /// Thêm/sửa/xoá LUẬT DUYỆT của project (ADR-062) — "loại việc X vào cột Y cần N chữ ký".
    ///
    /// <para>
    /// Cùng mức với <see cref="ManageBoardColumns"/>/<see cref="ManageFieldDefinitions"/>/
    /// <see cref="ManageWorkItemTypes"/>: đây là lược đồ mà cả đội chịu tác động, không phải
    /// dữ liệu của một task.
    /// </para>
    /// <para>
    /// 🔴 <b>KHÔNG dùng action này để gác việc QUYẾT ĐỊNH trên một yêu cầu duyệt.</b> Quyền
    /// quyết định đi theo <c>ApproverMode</c> của chính luật đó, không theo
    /// <c>RoleInProject</c> — ngoại lệ có chủ đích thứ hai của mô hình hai tầng (thứ nhất là
    /// <c>Notification</c>, ADR-023). Người dựng luật và người ký duyệt là hai vai khác nhau,
    /// và gộp chúng lại sẽ khiến mọi PM tự ký được luật của chính mình.
    /// </para>
    /// </summary>
    ManageApprovalPolicies,

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
