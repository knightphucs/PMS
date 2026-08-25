using PMS.Application.Common.Filtering;
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

    /// <summary>
    /// Danh sách task của project sau khi áp một <see cref="TaskQuerySpec"/> — nguồn của màn
    /// danh sách và của mọi "hàng đợi" (ADR-061).
    ///
    /// <para>
    /// 🔑 Điều kiện trên trường TUỲ BIẾN so <b>đúng kiểu</b> (<c>ValueNumber</c>/<c>ValueDate</c>
    /// /<c>ValueText</c>), không so chuỗi — đây là chỗ quyết định "cột có kiểu thay vì một cột
    /// JSON" của ADR-059 được thu hồi vốn.
    /// </para>
    /// <para>
    /// ⚠️ Không tái dùng <see cref="GetPagedByProjectAsync"/>: cái đó nhận
    /// <c>PagedRequest.SortBy</c> dạng chuỗi tự do với đúng bốn nhánh, còn ở đây sắp xếp đi
    /// theo <see cref="Domain.Enums.TaskField"/> (danh mục ĐÓNG) và còn phải gánh bộ lọc.
    /// Nhồi cả hai vào một hàm sẽ tạo một chữ ký mà mỗi caller chỉ dùng một nửa.
    /// </para>
    /// </summary>
    Task<PagedResult<TaskItem>> QueryAsync(
        Guid projectId, TaskQuerySpec spec, PagedRequest request, CancellationToken ct = default);
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

    /// <summary>
    /// Mọi việc chưa xong được giao cho một người, xuyên mọi dự án. Bao gồm task quá hạn,
    /// hôm nay, tương lai và chưa đặt hạn — nguồn của màn "Việc của tôi" (ADR-053).
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetMyOpenAssignedTasksAsync(
        Guid employeeId, CancellationToken ct = default);
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

    // ---------- Cổng yêu cầu (ADR-063) ----------

    /// <summary>
    /// Các yêu cầu do <paramref name="reporterId"/> gửi — xuyên dự án, mới nhất trước.
    ///
    /// <para>
    /// 🔑 <b>Vị từ <c>ReporterId == reporterId</c> CHÍNH LÀ phép phân quyền</b>, không phải
    /// một bộ lọc tiện lợi chồng lên một lượt kiểm khác. Cùng khuôn với
    /// <see cref="GetMyOpenAssignedTasksAsync"/> (ADR-053) — xem XML doc ở
    /// <c>TaskService.GetMyWorkAsync</c> để biết vì sao khuôn đó hợp lệ.
    /// </para>
    /// <para>
    /// ⚠️ Chỉ trả task được tạo QUA CỔNG, nhận diện bằng <c>WorkItemType.IsRequestable</c>.
    /// Không lọc theo cờ đó thì màn "Yêu cầu của tôi" sẽ hiện cả task nội bộ mà chính người
    /// đó tạo trong project họ là thành viên — đúng nhưng không phải thứ màn này nói nó là.
    /// </para>
    /// </summary>
    Task<PagedResult<TaskItem>> GetRequestsByReporterAsync(
        Guid reporterId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Một yêu cầu của chính người gửi, kèm mọi thứ màn chi tiết chỉ-đọc cần (project, loại,
    /// cột, giá trị trường, yêu cầu duyệt còn hiệu lực).
    /// <para>
    /// Trả <c>null</c> khi id không tồn tại <b>hoặc</b> người gọi không phải người gửi —
    /// hai ca không phân biệt được từ phía client, đúng chủ đích (guard G3).
    /// </para>
    /// </summary>
    Task<TaskItem?> GetRequestForReporterAsync(
        Guid taskId, Guid reporterId, CancellationToken ct = default);
}
