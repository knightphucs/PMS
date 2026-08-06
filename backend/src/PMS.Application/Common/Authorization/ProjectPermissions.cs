using PMS.Domain.Enums;

namespace PMS.Application.Common.Authorization;

public static class ProjectPermissions
{
    public static bool IsAllowed(ProjectAction action, RoleInProject role) => action switch
    {
        ProjectAction.View             => true,
        ProjectAction.Update
        or ProjectAction.Delete
        or ProjectAction.ManageMembers => role is RoleInProject.ProjectManager,
        // ADR-039: Member ĐƯỢC xem thống kê. Trước 2026-08-03 chỉ PM + Viewer — đọc §10
        // theo nghĩa đen thì đúng, nhưng đây không phải ranh giới bảo mật: Member vốn đã
        // đọc được mọi task ở mọi trạng thái qua ProjectAction.View trên /board. Tổng hợp
        // của dữ liệu đã đọc được không phải một đặc quyền.
        ProjectAction.ViewStatistics   => role is RoleInProject.ProjectManager
                                               or RoleInProject.Member
                                               or RoleInProject.Viewer,

        // Tạo/sửa/xóa task, gán người khác, quản lý sprint: chỉ PM (§10 + seq-01/seq-02).
        ProjectAction.CreateTask
        or ProjectAction.UpdateTask
        or ProjectAction.DeleteTask
        or ProjectAction.ManageAssignees
        or ProjectAction.ManageSprint
        // Cấu hình cột board (ADR-052): chỉ PM. Một Member xóa nhầm cột là đẩy hàng chục
        // task của người khác sang chỗ họ không ngờ tới.
        or ProjectAction.ManageBoardColumns => role is RoleInProject.ProjectManager,

        ProjectAction.CreateSubtask => role is RoleInProject.ProjectManager
                                            or RoleInProject.Member,

        // Tự nhận/tự rút: Member làm được để không phải chờ PM gán (mô hình Kanban),
        // Viewer thì không vì Viewer chỉ đọc.
        ProjectAction.SelfAssign       => role is RoleInProject.ProjectManager
                                               or RoleInProject.Member,

        // §10: Member "viết comment" là quyền được liệt kê tường minh; Viewer chỉ xem
        // (stakeholder/khách hàng/auditor theo dõi tiến độ, không tham gia thảo luận).
        // ĐỌC comment thì đi qua ProjectAction.View nên Viewer vẫn đọc được.
        ProjectAction.CreateComment    => role is RoleInProject.ProjectManager
                                               or RoleInProject.Member,

        // Đính kèm file (ADR-035), gắn nhãn (ADR-037), tạo liên kết task (ADR-038): cùng
        // một mức "cộng tác trên nội dung công việc" với CreateComment. Viewer chỉ đọc —
        // và ĐỌC ở cả ba đều đi qua ProjectAction.View nên Viewer vẫn xem/tải được.
        ProjectAction.UploadAttachment
        or ProjectAction.ManageTaskLabels
        or ProjectAction.ManageTaskLinks => role is RoleInProject.ProjectManager
                                                 or RoleInProject.Member,

        // Theo dõi task: cả Viewer cũng được. Đây là thao tác ghi duy nhất mà Viewer làm
        // được, và hợp lý — nó chỉ ảnh hưởng tới hộp thông báo của chính người đó, không
        // ai khác nhìn thấy. Vẫn tách thành action riêng chứ không mượn View (ADR-036).
        ProjectAction.Watch            => true,

        _ => false
    };
}
