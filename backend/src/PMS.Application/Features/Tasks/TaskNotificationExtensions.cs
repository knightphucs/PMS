using PMS.Domain.Entities;

namespace PMS.Application.Features.Tasks;

public static class TaskNotificationExtensions
{
    /// <summary>
    /// Những người cần biết khi có gì đó xảy ra với task: đang làm (assignee), đang theo dõi
    /// (watcher), và người đã tạo task (reporter). <c>NotifyMany</c> tự loại người thực hiện
    /// và tự distinct nên caller chỉ cần gộp danh sách.
    /// <para>
    /// Trước đây là <c>private static</c> trong <c>TaskStatusTransitionService</c>. Tách ra
    /// khi Comment cần đúng danh sách này cho <c>NotificationType.CommentAdded</c> — hai bản
    /// sao sẽ lệch nhau ngay lần đầu ai đó thêm một nhóm người nhận mới.
    /// </para>
    /// <para>
    /// ⚠️ Task phải được nạp kèm <c>Assignments</c> và <c>Watchers</c>
    /// (<c>ITaskRepository.GetWithNotificationTargetsAsync</c> hoặc
    /// <c>GetForStatusChangeAsync</c>) — collection chưa nạp thì rỗng và thông báo âm thầm
    /// gửi thiếu người, không lỗi gì cả.
    /// </para>
    /// </summary>
    public static IEnumerable<Guid> InterestedEmployeeIds(this TaskItem task)
        => task.Assignments.Select(a => a.EmployeeId)
               .Concat(task.Watchers.Select(w => w.EmployeeId))
               .Append(task.ReporterId);
}
