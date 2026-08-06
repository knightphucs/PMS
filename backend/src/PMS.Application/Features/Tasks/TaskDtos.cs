using PMS.Application.Features.BoardColumns;
using PMS.Application.Features.Labels;
using PMS.Domain.Enums;

namespace PMS.Application.Features.Tasks;

public record CreateTaskRequest(
    string Name,
    Guid ProjectId,
    Guid? SprintId,
    Guid? ParentTaskId,
    DateTime? DueDate,
    Priority Priority,
    string? Description = null,
    /// <summary>
    /// Cột đích khi bấm "+" trên MỘT cột cụ thể (2026-08-06). <c>null</c> = cột trái nhất
    /// của project (hành vi cũ, ADR-052) — nút "Tạo task" chung và tạo subtask đều gửi
    /// <c>null</c>. Phải cùng project với <see cref="ProjectId"/>, không thì 404.
    /// </summary>
    Guid? BoardColumnId = null);

public record UpdateTaskRequest(
    string Name,
    DateTime? DueDate,
    Priority Priority,
    byte[] RowVersion,
    string? Description = null);

public record MoveTaskToSprintRequest(Guid? SprintId);

/// <summary>Ghim/gỡ ghim — task ghim luôn đứng đầu cột trên board (2026-08-06).</summary>
public record PinTaskRequest(bool Pinned);

public record AssignTaskRequest(Guid EmployeeId, RoleInTask Role);

/// <summary>
/// Người đảm nhận, rút gọn cho THẺ trên board/backlog — chỉ đủ để vẽ avatar.
/// Khác <see cref="TaskAssigneeResponse"/> ở chỗ bỏ <c>RoleInTask</c> và
/// <c>AssignedDate</c>: board trả về hàng chục task một lượt, mỗi byte thừa nhân lên
/// theo số thẻ. Cần đầy đủ thì gọi <c>GET /tasks/{id}/assignees</c>.
/// </summary>
public record TaskCardAssignee(Guid EmployeeId, string EmployeeName);

/// <summary>
/// Trạng thái đính trên MỘT task — rút gọn từ <c>BoardColumn</c> (ADR-052).
///
/// <para>
/// Cố ý bỏ <c>Order</c>: thẻ không cần biết cột đứng thứ mấy, và board trả về hàng chục
/// thẻ một lượt nên mỗi trường thừa nhân lên theo số thẻ — cùng lý do
/// <see cref="TaskCardAssignee"/> đã bị cắt gọn. Cần danh sách cột đầy đủ (để dựng ô chọn
/// hay quản lý cột) thì gọi <c>GET /projects/{id}/columns</c>.
/// </para>
/// <para>
/// 🔴 <c>Category</c> phải có mặt ở đây: frontend cần biết task đã kết thúc chưa để gạch
/// ngang tên, ẩn nút, tô màu quá hạn — và nó <b>không được suy từ TÊN cột</b>, vì tên là
/// chuỗi do người dùng đặt.
/// </para>
/// </summary>
public record TaskStatusRef(Guid ColumnId, string Name, string Color, StatusCategory Category);

public record TaskSummaryResponse(
    Guid Id,
    /// <summary>Số thứ tự trong project. Cần khi muốn sắp xếp hoặc tra cứu bằng số.</summary>
    int Number,
    /// <summary>
    /// Mã hiển thị đã ghép sẵn, dạng <c>PMS-12</c>. Backend ghép chứ không trả rời
    /// <c>ProjectKey</c> + <c>Number</c> để frontend tự nối: hai nơi định dạng thì chắc
    /// chắn có lúc lệch nhau (ADR-034).
    /// </summary>
    string Code,
    string Name,
    TaskStatusRef Status,
    Priority Priority,
    DateTime? DueDate,
    bool IsOverdue,
    Guid? SprintId,
    Guid? ParentTaskId,
    decimal SubtaskProgress,
    /// <summary>
    /// Số subtask TRỰC TIẾP (không đệ quy — subtask chỉ có một cấp, ADR §5). Khác
    /// <c>SubtaskProgress</c> ở chỗ phân biệt được "không có subtask" (<c>0</c>) với "có
    /// subtask nhưng chưa xong cái nào" (<c>SubtaskProgress == 0</c> nhưng
    /// <c>SubtaskCount &gt; 0</c>) — thẻ Kanban cần biết đúng cái này để quyết định có vẽ
    /// nút mở rộng subtask hay không.
    /// </summary>
    int SubtaskCount,
    /// <summary>Ghim — luôn đứng đầu cột trên board bất kể độ ưu tiên (2026-08-06).</summary>
    bool IsPinned,
    IReadOnlyList<TaskCardAssignee> Assignees,
    IReadOnlyList<LabelResponse> Labels);

/// <summary>
/// Một task trong màn "Việc của tôi" — task tóm tắt kèm DỰ ÁN chứa nó.
///
/// <para>
/// Đây là endpoint XUYÊN DỰ ÁN duy nhất của hệ thống, nên nó là chỗ duy nhất mà
/// <c>TaskSummaryResponse</c> không đủ: mọi endpoint task khác đều nằm dưới
/// <c>/projects/{id}/…</c> nên client đã biết project từ URL, còn ở đây thì không.
/// </para>
/// </summary>
public record MyTaskResponse(
    TaskSummaryResponse Task,
    Guid ProjectId,
    string ProjectName,
    string ProjectKey);

/// <summary>
/// Kết quả gom sẵn theo dự án. Gom ở SERVER chứ không để client tự <c>groupBy</c>: thứ tự
/// dự án và cách đếm phải giống nhau giữa mọi màn hình, và client gom sẽ phải tự quyết định
/// những thứ đó một lần nữa.
/// </summary>
public record MyWorkGroup(
    Guid ProjectId,
    string ProjectName,
    string ProjectKey,
    IReadOnlyList<TaskSummaryResponse> Tasks);

public record MyWorkResponse(
    /// <summary>Mốc "hôm nay" của SERVER theo UTC. Trả về để client hiển thị nhất quán
    /// thay vì tự tính lại rồi lệch múi giờ (ADR-046b); không dùng để lọc task.</summary>
    DateTime Today,
    int TotalTasks,
    int OverdueTasks,
    IReadOnlyList<MyWorkGroup> Groups);

public record TaskAssigneeResponse(
    Guid EmployeeId,
    string EmployeeName,
    RoleInTask RoleInTask,
    DateTime AssignedDate);

/// <summary>
/// Một cột trên board kèm các task trong đó.
///
/// ⚠️ Tên cũ của record này là <c>BoardColumn</c>; đổi thành <c>BoardColumnGroup</c> vì
/// ADR-052 đưa <c>BoardColumn</c> thành một ENTITY thật trong <c>PMS.Domain.Entities</c>.
/// Giữ nguyên tên sẽ là hai kiểu cùng tên ở hai namespace mà file nào cũng <c>using</c> cả
/// hai — kiểu va chạm chỉ hiện ra bằng lỗi biên dịch khó đọc.
/// </summary>
public record BoardColumnGroup(BoardColumnResponse Column, IReadOnlyList<TaskSummaryResponse> Tasks);

/// <summary>
/// Board <b>luôn trả đủ MỌI cột của project</b>, kể cả cột rỗng — hợp đồng này có từ trước
/// ADR-052 và không đổi, chỉ khác là số cột nay do người dùng quyết định chứ không cố định 4.
/// Frontend không phải tự dựng cột thiếu, và thứ tự trái→phải lấy từ <c>Column.Order</c>.
/// </summary>
public record BoardResponse(
    Guid ProjectId, Guid? SprintId, IReadOnlyList<BoardColumnGroup> Columns);

public record TaskDetailResponse(
    Guid Id,
    int Number,
    string Code,
    string Name,
    string? Description,
    TaskStatusRef Status,
    Priority Priority,
    DateTime? DueDate,
    bool IsOverdue,
    Guid ProjectId,
    string ProjectKey,
    Guid? SprintId,
    Guid? ParentTaskId,
    Guid ReporterId,
    string ReporterName,
    IReadOnlyList<TaskAssigneeResponse> Assignees,
    IReadOnlyList<TaskSummaryResponse> Subtasks,
    IReadOnlyList<LabelResponse> Labels,
    /// <summary>
    /// Người gọi có đang theo dõi task này không — phụ thuộc NGƯỜI HỎI nên không suy ra
    /// được từ entity, phải truyền employeeId vào mapper (ADR-036).
    /// </summary>
    bool IsWatching,
    decimal SubtaskProgress,
    byte[] RowVersion);
