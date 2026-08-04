using PMS.Domain.Common;

namespace PMS.Domain.Entities;

/// <summary>
/// File đính kèm (hình ảnh, tài liệu) của một Task <b>hoặc</b> một Project — ADR-035.
/// <para>
/// Subtask không cần xử lý riêng: subtask là một <see cref="TaskItem"/> đầy đủ nên nó
/// đính kèm được y hệt task cha.
/// </para>
/// <para>
/// 🔴 <b>Đúng một trong hai FK được khác null.</b> Bảo đảm ở ba tầng: hai factory tĩnh dưới
/// đây (không có constructor public để lách), CHECK constraint
/// <c>CK_Attachments_ExactlyOneOwner</c> ở tầng database, và query filter loại bỏ attachment
/// của Task/Project đã xóa mềm. Dùng hai FK thật thay vì cặp
/// <c>(TargetKind, TargetId)</c> đa hình để giữ được ràng buộc khóa ngoại và query filter —
/// đúng nguyên tắc "bảo đảm bằng cấu trúc hơn bằng kỷ luật lập trình viên" của ADR-008/023.
/// </para>
/// </summary>
public class Attachment : BaseEntity
{
    /// <summary>Tên gốc do người dùng tải lên. CHỈ để hiển thị và đặt tên lúc tải về — tuyệt đối không dùng để dựng đường dẫn.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Tên trên đĩa, dạng <c>{guid}{ext}</c> do <c>IFileStorage</c> tự sinh. Người gọi không bao giờ cung cấp giá trị này.</summary>
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>Content-type đã qua whitelist. Lưu để tra cứu/thống kê — endpoint tải về vẫn trả <c>application/octet-stream</c> (ADR-035).</summary>
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public Guid UploaderId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? ProjectId { get; set; }

    public Employee Uploader { get; set; } = null!;
    public TaskItem? Task { get; set; }
    public Project? Project { get; set; }

    // Id sinh phía application: ApplyIdNeverGenerated() đặt ValueGeneratedNever() cho mọi
    // BaseEntity.Id, nên bản ghi thứ hai với Guid.Empty sẽ vi phạm khóa chính.
    public static Attachment ForTask(
        Guid taskId, Guid uploaderId, string fileName, string storedFileName,
        string contentType, long sizeBytes) => new()
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            UploaderId = uploaderId,
            FileName = fileName,
            StoredFileName = storedFileName,
            ContentType = contentType,
            SizeBytes = sizeBytes
        };

    public static Attachment ForProject(
        Guid projectId, Guid uploaderId, string fileName, string storedFileName,
        string contentType, long sizeBytes) => new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UploaderId = uploaderId,
            FileName = fileName,
            StoredFileName = storedFileName,
            ContentType = contentType,
            SizeBytes = sizeBytes
        };
}
