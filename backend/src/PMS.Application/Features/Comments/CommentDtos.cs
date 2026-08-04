namespace PMS.Application.Features.Comments;

/// <summary>
/// TaskId nằm ở route, không ở body — cùng lý do <c>CreateTaskRequest</c> không cho client
/// khai ReporterId: chỗ nào đã xác định được từ ngữ cảnh thì không mở cho client tự khai.
/// </summary>
public record CreateCommentRequest(string Content)
{
    /// <summary>
    /// Những người được nhắc tên (@mention) trong <see cref="Content"/> — do CLIENT gửi lên.
    ///
    /// <para>
    /// 🔴 Server cố ý <b>KHÔNG parse `@tên` từ nội dung</b>. Tên hiển thị không phải định
    /// danh: hai người có thể trùng tên, tên đổi được, và một chuỗi trông như `@ai` có thể
    /// chỉ là một địa chỉ email hay một đoạn code. Đoán định danh từ chuỗi hiển thị là sai
    /// từ gốc, và sai theo kiểu âm thầm gửi thông báo cho nhầm người.
    /// </para>
    /// <para>
    /// Client vốn đã BIẾT chính xác id — nó lấy từ chính ô gợi ý mà người dùng vừa chọn.
    /// Việc của server là <b>lọc</b>: chỉ giữ lại người thật sự là thành viên đang hoạt động
    /// của dự án, để danh sách này không thành kênh gửi thông báo tới người ngoài.
    /// </para>
    /// </summary>
    public IReadOnlyList<Guid> MentionedEmployeeIds { get; init; } = [];
}

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
