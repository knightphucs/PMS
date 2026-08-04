using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IProjectMemberRepository : IRepository<ProjectMember>
{
    Task<IReadOnlyList<ProjectMember>> GetPendingInvitationsAsync(
        Guid employeeId, CancellationToken ct = default);

    /// <summary>
    /// Lọc <paramref name="candidateIds"/> xuống còn những người THẬT SỰ là thành viên đang
    /// hoạt động (<c>InvitationStatus == Accepted</c>) của project.
    /// <para>
    /// Dùng cho @mention: danh sách người được nhắc do client gửi lên, nên nó phải được lọc
    /// ở server — nếu không, bất kỳ ai cũng bắn được thông báo tới bất kỳ ai bằng cách nhét
    /// id lạ vào body, và người nhận sẽ thấy tên một task họ không có quyền mở.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Guid>> FilterActiveMemberIdsAsync(
        Guid projectId, IReadOnlyCollection<Guid> candidateIds, CancellationToken ct = default);
}