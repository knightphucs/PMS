namespace PMS.Infrastructure.Storage;

/// <summary>
/// Cấu hình lưu trữ file, bind từ section <c>"FileStorage"</c> của appsettings.
/// </summary>
public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Thư mục gốc chứa file tải lên. 🔴 Phải nằm <b>NGOÀI</b> <c>wwwroot</c>.
    /// <para>
    /// Hiện <c>Program.cs</c> không hề gọi <c>UseStaticFiles()</c> và dự án không có
    /// <c>wwwroot</c> — đó là một <b>bất biến</b>, không phải tình cờ. File tải lên chỉ ra
    /// ngoài qua endpoint download có kiểm quyền; thêm static file serving là mở đường cho
    /// một payload HTML/SVG được phục vụ nguyên trạng trên chính origin của API.
    /// </para>
    /// </summary>
    public string Root { get; set; } = "App_Data/attachments";

    public long MaxFileBytes { get; set; } = 20 * 1024 * 1024;   // 20 MB

    /// <summary>Whitelist phần mở rộng, viết thường kèm dấu chấm.</summary>
    public string[] AllowedExtensions { get; set; } =
    [
        ".png", ".jpg", ".jpeg", ".gif", ".webp",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".csv", ".zip"
    ];

    /// <summary>Whitelist content-type. Kiểm song song với đuôi, không thay thế cho nhau.</summary>
    public string[] AllowedContentTypes { get; set; } =
    [
        "image/png", "image/jpeg", "image/gif", "image/webp",
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain", "text/csv",
        "application/zip", "application/x-zip-compressed"
    ];
}
