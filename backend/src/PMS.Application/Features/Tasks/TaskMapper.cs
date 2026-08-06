using PMS.Application.Features.Labels;
using PMS.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.Tasks;

/// <summary>
/// 🔴 <b><c>ToSummary</c> và <c>ToDetail</c> viết TAY, không để Mapperly sinh</b> — cùng lý
/// do <c>ProjectMapper.ToSummary</c> phải viết tay (ADR-032), nay áp cho mã task (ADR-034).
/// <para>
/// Mã hiển thị <c>PMS-12</c> cần <c>Project.Key</c>, mà <c>Project</c> không phải lúc nào
/// cũng được <c>Include</c>. Đặt nó thành computed property trên <see cref="TaskItem"/> sẽ
/// NRE ở mọi query board/backlog/paged, hoặc — tệ hơn — buộc mọi query đó phải nhớ thêm
/// một <c>Include</c>. Đó đúng là lớp lỗi đã xảy ra hai lần trong dự án này
/// (<c>SubtaskProgress</c> luôn trả 0 vì thiếu Include; <c>Assignee.Employee</c> NRE).
/// </para>
/// <para>
/// Bắt <c>projectKey</c> làm tham số bắt buộc thì trình biên dịch chặn ngay tại call site,
/// không cần ai phải nhớ. Service lấy key một lần cho cả request rồi truyền xuống.
/// </para>
/// </summary>
[Mapper]
public partial class TaskMapper
{
#pragma warning disable RMG020 // Source member is not mapped to any target member
    [MapProperty(nameof(TaskAssignment.Employee.Name), nameof(TaskCardAssignee.EmployeeName))]
    public partial TaskCardAssignee ToCardAssignee(TaskAssignment assignment);

    [MapProperty(nameof(TaskAssignment.Employee.Name), nameof(TaskAssigneeResponse.EmployeeName))]
    public partial TaskAssigneeResponse ToAssigneeResponse(TaskAssignment assignment);

    public partial LabelResponse ToLabelResponse(Label label);
#pragma warning restore RMG020 // Source member is not mapped to any target member

    /// <summary>Ghép mã hiển thị. Một chỗ duy nhất định dạng — xem chú thích của lớp.</summary>
    public static string FormatCode(string projectKey, int number) => $"{projectKey}-{number}";

    /// <summary>
    /// Trạng thái đính trên thẻ (ADR-052).
    ///
    /// <para>
    /// 🔴 <b>Đọc <c>task.BoardColumn</c> nên MỌI query nuôi mapper này bắt buộc phải
    /// <c>Include(t =&gt; t.BoardColumn)</c>.</b> Đây đúng là lớp lỗi mà chú thích của lớp
    /// đã kể (<c>SubtaskProgress</c> luôn trả 0 vì thiếu Include), nên lần này chọn ném rõ
    /// ràng thay vì trả dữ liệu sai im lặng: thiếu Include thì <c>BoardColumn</c> là
    /// <c>null</c> và câu lệnh dưới ném <c>NullReferenceException</c> ngay ở request đầu tiên.
    /// </para>
    /// <para>
    /// 📌 Vì sao không đọc <c>task.Category</c> (trường lưu cứng, luôn có sẵn): nó chỉ mang
    /// NHÓM, không mang tên và màu cột — mà thẻ cần cả ba. Hai nguồn phục vụ hai việc khác
    /// nhau: <c>Category</c> cho phép LỌC/TÍNH trong SQL, <c>BoardColumn</c> cho HIỂN THỊ.
    /// </para>
    /// </summary>
    public static TaskStatusRef ToStatusRef(TaskItem task) => new(
        task.BoardColumnId,
        task.BoardColumn.Name,
        task.BoardColumn.Color,
        task.BoardColumn.Category);

    public TaskSummaryResponse ToSummary(TaskItem task, string projectKey) => new(
        task.Id,
        task.Number,
        FormatCode(projectKey, task.Number),
        task.Name,
        ToStatusRef(task),
        task.Priority,
        task.StoryPoints,
        task.DueDate,
        task.IsOverdue,
        task.SprintId,
        task.ParentTaskId,
        task.SubtaskProgress,
        task.Subtasks.Count,
        task.IsPinned,
        task.Assignments.Select(ToCardAssignee).ToList(),
        task.Labels.Select(ToLabelResponse).ToList());

    /// <param name="currentEmployeeId">
    /// Người đang hỏi — quyết định <c>IsWatching</c>. Bắt buộc vì giá trị này phụ thuộc
    /// người gọi chứ không phải entity; để mặc định sẽ ra <c>false</c> im lặng cho mọi người.
    /// </param>
    public TaskDetailResponse ToDetail(TaskItem task, string projectKey, Guid currentEmployeeId) => new(
        task.Id,
        task.Number,
        FormatCode(projectKey, task.Number),
        task.Name,
        task.Description,
        ToStatusRef(task),
        task.Priority,
        task.StoryPoints,
        task.DueDate,
        task.IsOverdue,
        task.ProjectId,
        projectKey,
        task.SprintId,
        task.ParentTaskId,
        task.ReporterId,
        task.Reporter.Name,
        task.Assignments.Select(ToAssigneeResponse).ToList(),
        // Subtask cùng project nên dùng lại đúng projectKey — subtask KHÔNG bao giờ nằm
        // ở project khác (TaskItem.AddSubtask gán ProjectId của cha cho con).
        task.Subtasks.Select(s => ToSummary(s, projectKey)).ToList(),
        task.Labels.Select(ToLabelResponse).ToList(),
        task.Watchers.Any(w => w.EmployeeId == currentEmployeeId),
        task.SubtaskProgress,
        task.RowVersion);
}
