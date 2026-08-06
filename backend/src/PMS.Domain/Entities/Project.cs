using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

public class Project : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Mã ngắn viết hoa của project (<c>PMS</c>, <c>KHO</c>…) — nửa đầu của mã task
    /// <c>PMS-12</c>. Duy nhất toàn hệ thống, KHÔNG tái sử dụng kể cả khi project bị xóa
    /// mềm: mã task đã phát tán ra comment/URL/tài liệu ngoài (ADR-033).
    /// Sinh tự động từ tên bằng <c>ProjectKeyGenerator</c>, người dùng không nhập.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
    public DateTime ExpectedCompletionDate { get; set; }
    public Status Status { get; private set; } = Status.ToDo;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>
    /// Số task đang hoạt động (chưa xóa mềm) của project.
    ///
    /// <para>
    /// 🔴 Duy trì bởi TRIGGER DB <c>trg_Tasks_MaintainProjectTaskCount</c>
    /// (xem migration <c>AddReportingDbObjects</c>), <b>không có dòng C# nào ghi vào đây</b>
    /// — đúng nghĩa đen: không có domain method nào set nó, `private set` chỉ để chặn lỡ tay.
    /// </para>
    /// <para>
    /// Đây là minh họa "trigger" cho hạng mục kỹ thuật DB của báo cáo — bản thân ứng dụng
    /// KHÔNG cần cột này, <c>ProjectStatisticsRepository.CountTasksAsync</c> đã tính đúng
    /// theo yêu cầu tại thời điểm hỏi. Cố tình nêu rõ ở đây thay vì giấu đi: đây là cột kém
    /// cần thiết nhất trong bốn đối tượng DB mới, tồn tại để trigger có việc thật để làm
    /// chứ không phải ngược lại.
    /// </para>
    /// </summary>
    public int TaskCount { get; private set; }

    public ICollection<TaskItem> Tasks { get; set; } = [];
    public ICollection<Sprint> Sprints { get; set; } = [];

    /// <summary>
    /// Các cột board của project (ADR-052). Luôn có ít nhất một cột — cấp sẵn bốn cột mặc
    /// định lúc tạo project, và <c>BoardColumnService</c> chặn xóa cột cuối cùng.
    /// </summary>
    public ICollection<BoardColumn> BoardColumns { get; set; } = [];

    public bool IsCompleted() => Status == Status.Done;

    /// <summary>
    /// Đánh dấu project đã hoàn thành.
    /// <para>
    /// ⚠️ Cho tới 2026-08-04, method này có đúng MỘT caller trong toàn bộ solution:
    /// <c>DbSeeder</c>. Nghĩa là mọi project tạo qua API vĩnh viễn nằm ở <c>ToDo</c>, trong
    /// khi <c>Status</c> vẫn được trả trong DTO và vẫn là khóa <c>sortBy</c> hợp lệ — một
    /// trường chết đội lốt tính năng. Nay có <c>POST /projects/{id}/complete</c>.
    /// </para>
    /// </summary>
    public void Complete()
    {
        // Idempotent chứ không ném: gọi lại trên project đã xong không phải một lỗi nghiệp
        // vụ, chỉ là không có gì để làm. Khác `Employee.Lock()` — ở đó "khóa cái đã khóa"
        // thường là dấu hiệu hai admin thao tác chồng nhau nên đáng báo.
        if (Status == Status.Done) return;
        Status = Status.Done;
    }

    /// <summary>
    /// Mở lại một project đã hoàn thành. Đưa về <see cref="Status.InProgress"/> chứ KHÔNG về
    /// <c>ToDo</c>: project từng chạy tới Done thì công việc đã diễn ra, quay về "chưa bắt
    /// đầu" là ghi lại một điều không đúng sự thật.
    /// </summary>
    public void Reopen()
    {
        if (Status != Status.Done)
            throw new DomainException("Chỉ mở lại được project đang ở trạng thái Hoàn thành.");

        Status = Status.InProgress;
    }

    public ICollection<ProjectMember> Members { get; set; } = [];

    public ProjectMember Invite(Employee employee, RoleInProject role)
    {
        var existing = Members.FirstOrDefault(m => m.EmployeeId == employee.Id);

        if (existing is not null)
        {
            if (existing.InvitationStatus != InvitationStatus.Declined)
                throw new DomainException(
                    "Người này đã là thành viên hoặc đang có lời mời chờ phản hồi.");

            existing.Reinvite(role);
            return existing;
        }

        var member = ProjectMember.Invite(Id, employee.Id, role);
        Members.Add(member);
        return member;
    }

    public void ChangeMemberRole(Guid employeeId, RoleInProject newRole)
    {
        var member = RequireMember(employeeId);

        if (member.RoleInProject == newRole) return;   // idempotent, không phải lỗi

        // Kiểm TRƯỚC khi đổi: nếu ném exception sau khi đã mutate, entity vẫn nằm trong
        // ChangeTracker ở trạng thái bẩn — một SaveChanges sau đó trên cùng DbContext sẽ
        // ghi xuống DB đúng cái state vừa bị từ chối.
        if (member.RoleInProject == RoleInProject.ProjectManager && member.IsActive())
            EnsureAnotherManagerExists(employeeId);

        member.ChangeRole(newRole);
    }

    public void RemoveMember(Guid employeeId)
    {
        var member = RequireMember(employeeId);

        if (member.RoleInProject == RoleInProject.ProjectManager && member.IsActive())
            EnsureAnotherManagerExists(employeeId);

        Members.Remove(member);
    }

    public RoleInProject? GetRoleOf(Guid employeeId)
        => Members.FirstOrDefault(m => m.EmployeeId == employeeId && m.IsActive())?.RoleInProject;

    private ProjectMember RequireMember(Guid employeeId)
        => Members.FirstOrDefault(m => m.EmployeeId == employeeId)
        ?? throw new DomainException("Người này không có trong danh sách thành viên của project.");

    private void EnsureAnotherManagerExists(Guid excludingEmployeeId)
    {
        var hasOther = Members.Any(m =>
            m.EmployeeId != excludingEmployeeId
            && m.RoleInProject == RoleInProject.ProjectManager
            && m.IsActive());

        if (!hasOther)
            throw new DomainException(
                "Project phải luôn còn ít nhất một Project Manager đang hoạt động.");
    }

    public static Project Create(
        string name, string description, DateTime expectedCompletionDate, Guid creatorId, string key)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Key = key.Trim().ToUpperInvariant(),
            Description = description.Trim(),
            ExpectedCompletionDate = expectedCompletionDate
        };

        project.Members.Add(ProjectMember.CreateOwner(project.Id, creatorId));

        // Cấp bốn cột mặc định ngay lúc tạo. Bắt buộc, không phải tiện ích: task mới cần một
        // `BoardColumnId` hợp lệ, nên một project không cột là project không tạo được task
        // nào — và lỗi đó sẽ chỉ lộ ra ở lần tạo task đầu tiên chứ không phải lúc tạo project.
        foreach (var column in BoardColumn.CreateDefaults(project.Id))
            project.BoardColumns.Add(column);

        return project;
    }
}
