using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Approvals;

/// <summary>
/// Luật "ai được ký" (ADR-062) — dùng chung bởi <see cref="ApprovalService"/> (đường quyết
/// định) và <c>TaskStatusTransitionService</c> (đường sinh yêu cầu + gửi thông báo).
///
/// <para>
/// 🔴 Tách ra đây chứ không chép hai lần, vì hai chỗ đó phải trả lời <b>cùng một câu hỏi</b>:
/// một người có nằm trong danh sách ký của cổng này không. Lệch nhau nghĩa là hệ thống gửi
/// thông báo cho một nhóm rồi từ chối đúng nhóm đó — đúng lớp lỗi "hai nơi cùng dựng một
/// thứ" mà ADR-034 đã trả giá.
/// </para>
/// </summary>
public static class ApprovalRules
{
    /// <summary>
    /// Những người được ký trên một cổng.
    ///
    /// <para>
    /// ⚠️ <see cref="ApproverMode.NamedApprovers"/> KHÔNG tự động thêm PM vào: đó chính là
    /// điểm của chế độ này ("chỉ trưởng phòng ký được"). Muốn PM ký được thì đổi chế độ,
    /// đừng nới luật ở đây.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Guid> ResolveApproverIds(ApprovalPolicy policy, Project project)
        => policy.ApproverMode switch
        {
            ApproverMode.NamedApprovers => policy.Approvers.Select(a => a.EmployeeId).Distinct().ToList(),
            _ => ActiveManagerIds(project).ToList(),
        };

    /// <summary>
    /// PM đang hoạt động của project. Cùng định nghĩa với <c>TaskAssignmentService</c> và
    /// <c>ProjectMemberService</c>: phải là <c>IsActive()</c>, nếu không một lời mời chưa
    /// được chấp nhận cũng thành một người ký hợp lệ.
    /// </summary>
    public static IEnumerable<Guid> ActiveManagerIds(Project project)
        => project.Members
                  .Where(m => m.RoleInProject == RoleInProject.ProjectManager && m.IsActive())
                  .Select(m => m.EmployeeId);
}
