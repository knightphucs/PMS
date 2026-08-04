using PMS.Application.Common.Models;
using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface ITaskRepository : IRepository<TaskItem>
{
    Task<TaskItem?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<TaskItem?> GetWithSubtasksAsync(Guid id, CancellationToken ct = default);

    /// <summary>Nạp kèm Assignments + Employee — dùng cho mọi thao tác gán/gỡ người.</summary>
    Task<TaskItem?> GetWithAssignmentsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Nạp kèm <c>Labels</c> có tracking — cần để thêm/bớt phần tử trong collection.</summary>
    Task<TaskItem?> GetWithLabelsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Nạp đúng những gì việc đổi trạng thái cần: Assignments (kiểm quyền theo ADR-017),
    /// Watchers (gửi thông báo) và Subtasks (để SubtaskProgress trong response không bị
    /// báo nhầm 0%). Nhẹ hơn GetWithDetailsAsync vốn kéo cả Comment/Label/Link.
    /// </summary>
    Task<TaskItem?> GetForStatusChangeAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Nạp đúng những gì <c>TaskNotificationExtensions.InterestedEmployeeIds</c> cần:
    /// Assignments + Watchers. Không tái dùng <see cref="GetForStatusChangeAsync"/> vì nó
    /// còn kéo Subtasks chỉ để tính SubtaskProgress — và cái tên sẽ nói sai việc khi
    /// CommentService gọi tới.
    /// </summary>
    Task<TaskItem?> GetWithNotificationTargetsAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<TaskItem>> GetPagedByProjectAsync(
        Guid projectId, PagedRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetBacklogAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Toàn bộ task gốc của project, không phân trang — dùng dựng Board dạng Kanban cho
    /// project không chạy theo sprint. Subtask bị loại: chúng hiện trong chi tiết task cha,
    /// không phải thẻ riêng trên board.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetRootTasksByProjectAsync(
        Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetBySprintAsync(Guid sprintId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetUnfinishedBlockersAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetOverdueAsync(CancellationToken ct = default);

    /// <summary>
    /// Task sắp đến hạn (trong vòng <paramref name="horizonDays"/> ngày) hoặc đã quá hạn,
    /// chưa <c>Done</c> — nạp kèm <c>Assignments</c> + <c>Watchers</c> cho background job.
    /// <para>
    /// 🔴 KHÔNG tái dùng <see cref="GetOverdueAsync"/> được: nó không <c>Include</c>
    /// <c>Assignments</c>/<c>Watchers</c>, nên <c>InterestedEmployeeIds()</c> sẽ chỉ trả về
    /// mỗi reporter và job âm thầm gửi thiếu người — không lỗi nào.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetDueSoonOrOverdueWithTargetsAsync(
        int horizonDays, CancellationToken ct = default);
    Task<int> CountActiveAssignedAsync(Guid projectId, Guid employeeId, CancellationToken ct = default);
}
