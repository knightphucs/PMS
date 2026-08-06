using PMS.Domain.Common;
using PMS.Domain.Enums;

namespace PMS.Domain.Entities;

public class TaskItem : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
    /// <summary>Ước lượng độ lớn công việc theo Story Point (0 = chưa ước lượng).</summary>
    public int StoryPoints { get; set; }

    /// <summary>Cột board mà task đang đứng — thay cho enum <c>Status</c> cũ (ADR-052).</summary>
    public Guid BoardColumnId { get; set; }
    public BoardColumn BoardColumn { get; set; } = null!;

    /// <summary>
    /// Nhóm ngữ nghĩa của cột hiện tại — <b>bản sao có chủ đích</b> của
    /// <c>BoardColumn.Category</c>.
    ///
    /// <para>
    /// 🔴 <b>Vì sao chấp nhận dữ liệu trùng, ngược với thói quen chuẩn hóa:</b> hai lý do,
    /// cả hai đều đã có tiền lệ trả giá trong dự án này.
    /// </para>
    /// <para>
    /// <b>1. Computed property đọc navigation là NRE chờ sẵn.</b> <see cref="IsOverdue"/> và
    /// <see cref="SubtaskProgress"/> phải biết task đã kết thúc chưa. Nếu chúng đọc
    /// <c>BoardColumn.Category</c> thì mọi query nào quên <c>Include(BoardColumn)</c> sẽ nổ
    /// lúc chạy — đúng cái bẫy mà comment về <c>Project.Key</c> ở trên đã mô tả, và là lý do
    /// mã task phải ghép ở Mapper chứ không ở entity (ADR-034).
    /// </para>
    /// <para>
    /// <b>2. EF dịch được thành SQL phẳng.</b> 39 chỗ trong solution hỏi "xong chưa" bằng
    /// <c>t.Status != Status.Done</c>; với cột này chúng thành <c>t.Category != Done</c> —
    /// đổi đúng một định danh, <b>không phải viết lại query nào</b> và không thêm JOIN vào
    /// những truy vấn nóng nhất (board, backlog, thống kê, job task quá hạn).
    /// </para>
    /// <para>
    /// ⚠️ Cái giá là nó <b>trôi được</b>. Chốt chặn: <c>private set</c>, và người ghi duy
    /// nhất là <see cref="MoveTo"/>. Khi sửa <c>Category</c> của một cột thì phải cập nhật
    /// mọi task trong cột đó — <c>BoardColumnService</c> chịu trách nhiệm, và có test khóa.
    /// </para>
    /// </summary>
    public StatusCategory Category { get; private set; } = StatusCategory.ToDo;

    /// <summary>
    /// Số thứ tự của task TRONG project — nửa sau của mã hiển thị kiểu Jira (<c>PMS-12</c>).
    /// <para>
    /// <c>private set</c> có chủ đích: chỉ <see cref="AssignNumber"/> đặt được, và nó chỉ
    /// được gọi đúng một lần trên đường tạo task. Số đã cấp thì không bao giờ đổi và không
    /// bao giờ tái sử dụng — kể cả khi task bị xóa mềm — vì mã task xuất hiện trong comment,
    /// URL và tài liệu bên ngoài hệ thống (ADR-033).
    /// </para>
    /// Mã đầy đủ KHÔNG được ghép ở đây: nó cần <c>Project.Key</c>, mà navigation
    /// <see cref="Project"/> không phải lúc nào cũng được Include — một computed property
    /// sẽ NRE ở mọi query board/backlog. Việc ghép nằm ở TaskMapper (ADR-034).
    /// </summary>
    public int Number { get; private set; }

    /// <summary>Chỉ dùng trên đường tạo task, sau khi đã lấy số từ ProjectTaskCounters.</summary>
    public void AssignNumber(int number)
    {
        if (number <= 0)
            throw new DomainException("Số thứ tự task phải là số dương.");
        Number = number;
    }

    public byte[] RowVersion { get; set; } = null!;

    public Guid ProjectId { get; set; }
    public Guid? SprintId { get; set; }
    public Guid? ParentTaskId { get; set; }
    public Guid ReporterId { get; set; }

    public Project Project { get; set; } = null!;
    public Sprint? Sprint { get; set; }
    public TaskItem? ParentTask { get; set; }
    public Employee Reporter { get; set; } = null!;
    public ICollection<TaskItem> Subtasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();
    public ICollection<Watcher> Watchers { get; set; } = new List<Watcher>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Label> Labels { get; set; } = new List<Label>();
    public ICollection<TaskLink> OutgoingLinks { get; set; } = new List<TaskLink>();
    public ICollection<TaskLink> IncomingLinks { get; set; } = new List<TaskLink>();

    // Id phải sinh phía application: PmsDbContext.ApplyIdNeverGenerated() đặt
    // ValueGeneratedNever() cho mọi BaseEntity.Id, nên để mặc định Guid.Empty thì
    // bản ghi thứ hai sẽ vi phạm khóa chính. Nhất quán với ProjectMember.Invite().
    public void AddAssignee(Employee employee, RoleInTask role)
    {
        if (Assignments.Any(a => a.EmployeeId == employee.Id)) return;
        Assignments.Add(new TaskAssignment
        {
            Id = Guid.NewGuid(),
            TaskId = Id, EmployeeId = employee.Id,
            // Gán luôn navigation để đồ thị đối tượng nhất quán ngay trong bộ nhớ: caller
            // map bản ghi vừa tạo ra DTO (cần Employee.Name) trước khi có lần load lại nào.
            // Employee truyền vào luôn là entity đã được EF track nên không bị hiểu nhầm
            // là muốn insert Employee mới.
            Employee = employee,
            RoleInTask = role, AssignedDate = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Gỡ 1 người khỏi task. Trả về false nếu người đó vốn không được gán —
    /// để Service phân biệt "không có gì để làm" với thao tác thật (chỉ ghi
    /// ActivityLog/Notification khi thật sự có thay đổi).
    /// </summary>
    public bool RemoveAssignee(Guid employeeId)
    {
        var assignment = Assignments.FirstOrDefault(a => a.EmployeeId == employeeId);
        if (assignment is null) return false;

        Assignments.Remove(assignment);
        return true;
    }

    public void LinkTo(TaskItem target, LinkType linkType)
        => OutgoingLinks.Add(new TaskLink
        {
            Id = Guid.NewGuid(),
            SourceTaskId = Id, TargetTaskId = target.Id, LinkType = linkType
        });

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public bool IsSubtask => ParentTaskId is not null;

    /// <summary>
    /// Chuyển task sang một cột khác. <b>Người ghi DUY NHẤT</b> của
    /// <see cref="BoardColumnId"/> và <see cref="Category"/> — hai trường đó phải luôn đi
    /// cùng nhau, nên không ai được đặt riêng lẻ.
    ///
    /// <para>
    /// 🔴 <b>KHÔNG còn ma trận chuyển trạng thái</b> (ADR-052 thay thế ADR-021). Trước đây
    /// <c>CanTransitionTo</c> liệt kê sáu cặp hợp lệ, và đó là luật đúng khi chỉ có bốn
    /// trạng thái do hệ thống định nghĩa. Với cột do <b>người dùng</b> tạo thì không còn cơ
    /// sở nào để nói cặp nào hợp lệ: hệ thống không biết "Chờ QA" đứng trước hay sau
    /// "Đang sửa". Ép một luật lên đó là đoán hộ quy trình của người khác.
    /// </para>
    /// <para>
    /// Hệ quả cần biết: <b>kéo thẻ về đúng cột nó đang đứng nay là no-op hợp lệ</b>, không
    /// còn 409. Guard duy nhất còn lại là "task bị chặn thì không được sang cột
    /// <see cref="StatusCategory.InProgress"/>", và nó nằm ở tầng Service vì cần truy vấn
    /// TaskLink chứ không đọc được từ entity này.
    /// </para>
    /// </summary>
    public void MoveTo(BoardColumn column)
    {
        // DomainException chứ không phải ArgumentException: middleware chỉ map
        // DomainException thành 409, ngoại lệ khác rơi vào catch-all và trả 500 (ADR-011).
        if (column.ProjectId != ProjectId)
            throw new DomainException("Không thể chuyển task sang cột của một project khác.");

        BoardColumnId = column.Id;
        BoardColumn = column;
        Category = column.Category;
    }

    /// <summary>
    /// Đồng bộ lại <see cref="Category"/> khi cột ĐỔI NHÓM mà task không đi đâu cả.
    /// Chỉ <c>BoardColumnService</c> gọi, ngay sau khi ghi nhóm mới lên cột.
    /// </summary>
    public void SyncCategory(StatusCategory category) => Category = category;

    // IsOverdue/SubtaskProgress là property computed (không lưu cứng - xem §5 ARCHITECTURE).
    // Để là property thay vì method vì Mapperly chỉ map được property; nhờ đó TaskMapper
    // không cần map thủ công. EF Core bỏ qua property get-only không có backing field —
    // IsSubtask ở dưới đã chứng minh điều đó chạy được với cấu hình hiện tại.
    //
    // 📌 Cả hai đọc `Category` (trường lưu cứng trên chính task) chứ KHÔNG đọc
    // `BoardColumn.Category` — xem chú thích dài ở `Category` để biết vì sao.
    public bool IsOverdue
        => DueDate.HasValue
           && DueDate.Value.Date < DateTime.UtcNow.Date
           && Category != StatusCategory.Done;

    public decimal SubtaskProgress
    {
        get
        {
            if (Subtasks.Count == 0) return 0m;
            var done = Subtasks.Count(s => s.Category == StatusCategory.Done);
            return Math.Round((decimal)done / Subtasks.Count * 100, 2);
        }
    }

    /// <summary>
    /// Ghim — task luôn đứng ĐẦU cột trên board, bất kể độ ưu tiên (2026-08-06).
    ///
    /// <para>
    /// Ghim CHUNG cho cả project, không phải riêng theo người xem: một cờ trên chính
    /// <see cref="TaskItem"/> là đủ, không cần bảng quan hệ Employee–Task riêng. Đây là
    /// hành động quản lý board (ai cũng thấy cùng một thứ tự), cùng nhóm quyền với
    /// <c>ProjectAction.UpdateTask</c> — không có action riêng cho việc này.
    /// </para>
    /// <para><c>private set</c>: chỉ <see cref="Pin"/>/<see cref="Unpin"/> ghi được.</para>
    /// </summary>
    public bool IsPinned { get; private set; }

    public void Pin() => IsPinned = true;
    public void Unpin() => IsPinned = false;

    public void AddSubtask(TaskItem child)
    {
        // DomainException chứ không phải InvalidOperationException: middleware chỉ map
        // DomainException thành 409, ngoại lệ khác rơi vào catch-all và trả 500 (ADR-011).
        if (IsSubtask)
            throw new DomainException(
                "Subtask không được có subtask con (chỉ 1 cấp cha–con).");
        child.ParentTaskId = Id;
        child.ProjectId = ProjectId;
        Subtasks.Add(child);
    }
}
