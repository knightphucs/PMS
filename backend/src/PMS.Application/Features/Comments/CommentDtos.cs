namespace PMS.Application.Features.Comments;

/// <summary>
/// TaskId nằm ở route, không ở body — cùng lý do <c>CreateTaskRequest</c> không cho client
/// khai ReporterId: chỗ nào đã xác định được từ ngữ cảnh thì không mở cho client tự khai.
/// </summary>
public record CreateCommentRequest(string Content);

/// <summary>
/// Không có RowVersion (ADR-026): chỉ tác giả sửa được comment của mình, nên không tồn tại
/// kịch bản hai người cùng ghi đè nhau — thứ mà ADR-016/021 sinh ra để chặn.
/// </summary>
public record UpdateCommentRequest(string Content);

/// <summary>
/// <paramref name="UpdatedAt"/> khác null nghĩa là comment đã được sửa — đủ để UI hiện nhãn
/// "đã chỉnh sửa" mà không cần cột riêng, vì <c>ApplyAuditFields()</c> đã đóng dấu sẵn (ADR-014).
/// </summary>
public record CommentResponse(
    Guid Id,
    Guid TaskId,
    Guid AuthorId,
    string AuthorName,
    string Content,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
