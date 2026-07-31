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
        ProjectAction.ViewStatistics   => role is RoleInProject.ProjectManager
                                               or RoleInProject.Viewer,

        // Tạo/sửa/xóa task, gán người khác, quản lý sprint: chỉ PM (§10 + seq-01/seq-02).
        ProjectAction.CreateTask
        or ProjectAction.UpdateTask
        or ProjectAction.DeleteTask
        or ProjectAction.ManageAssignees
        or ProjectAction.ManageSprint  => role is RoleInProject.ProjectManager,

        // Tự nhận/tự rút: Member làm được để không phải chờ PM gán (mô hình Kanban),
        // Viewer thì không vì Viewer chỉ đọc.
        ProjectAction.SelfAssign       => role is RoleInProject.ProjectManager
                                               or RoleInProject.Member,

        // §10: Member "viết comment" là quyền được liệt kê tường minh; Viewer chỉ xem
        // (stakeholder/khách hàng/auditor theo dõi tiến độ, không tham gia thảo luận).
        // ĐỌC comment thì đi qua ProjectAction.View nên Viewer vẫn đọc được.
        ProjectAction.CreateComment    => role is RoleInProject.ProjectManager
                                               or RoleInProject.Member,
        _ => false
    };
}
