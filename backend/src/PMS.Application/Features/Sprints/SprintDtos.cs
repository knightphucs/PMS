using PMS.Domain.Enums;

namespace PMS.Application.Features.Sprints;

public record CreateSprintRequest(string Name, string Goal, DateTime StartDate, DateTime EndDate);

/// <summary>
/// Đóng sprint (ADR-050) — <b>hỏi task chưa xong đi đâu, không tự quyết hộ</b>.
///
/// <para>
/// <c>TargetSprintId = null</c> nghĩa là đẩy về Backlog. Trường này <b>bắt buộc có mặt
/// trong thân request</b> ngay cả khi là <c>null</c>: một body rỗng và "chọn Backlog" phải
/// phân biệt được, nếu không thì client quên gửi sẽ âm thầm biến thành một lựa chọn.
/// </para>
/// <para>
/// Sprint không còn task chưa xong thì trường này bị bỏ qua.
/// </para>
/// </summary>
public record CompleteSprintRequest(Guid? TargetSprintId);

/// <summary>
/// Xem trước việc đóng sprint. Frontend gọi trước khi mở dialog để biết <i>có bao nhiêu
/// task chưa xong</i> và <i>có thể đẩy sang những sprint nào</i> — hỏi mà không cho biết
/// hai điều đó thì người dùng không có cơ sở nào để chọn.
/// </summary>
public record SprintCompletionPreview(
    Guid SprintId,
    string SprintName,
    int DoneCount,
    int UnfinishedCount,
    IReadOnlyList<SprintOption> AvailableTargets);

public record SprintOption(Guid Id, string Name, DateTime StartDate, DateTime EndDate);

public record UpdateSprintRequest(string Name, string Goal, DateTime StartDate, DateTime EndDate);

/// <summary>
/// Sprint chỉ có một dạng response (không tách Summary/Detail như Project) vì entity nhỏ,
/// hai bản sẽ trùng nhau hoàn toàn. Danh sách task của sprint lấy qua endpoint Board —
/// đó mới là cách nhìn đúng của một sprint.
/// </summary>
public record SprintResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Goal,
    DateTime StartDate,
    DateTime EndDate,
    /// <summary>
    /// ⚠️ Suy từ NGÀY (hôm nay có nằm trong khoảng không), KHÔNG phải
    /// <c>Status == Active</c>. Hai thứ khác nhau và đều có ích: một sprint quá hạn mà chưa
    /// ai đóng thì <c>IsActive = false</c> nhưng <c>Status = Active</c> — chính là tín hiệu
    /// "cần đóng sổ" mà giao diện nên nhắc.
    /// </summary>
    bool IsActive,
    int TaskCount,
    SprintStatus Status,
    DateTime? CompletedAt,
    /// <summary>Số task trong sprint đã thuộc cột nhóm <c>Done</c> — nuôi thanh tiến độ.</summary>
    int DoneCount);
