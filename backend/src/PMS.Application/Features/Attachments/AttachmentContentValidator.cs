using PMS.Application.Common.Exceptions;

namespace PMS.Application.Features.Attachments;

/// <summary>Cấu hình whitelist mà tầng Application cần — implementation nằm ở Infrastructure.</summary>
public interface IAttachmentPolicy
{
    long MaxFileBytes { get; }
    IReadOnlyCollection<string> AllowedExtensions { get; }
    IReadOnlyCollection<string> AllowedContentTypes { get; }
}

/// <summary>
/// Toàn bộ luật kiểm file tải lên (ADR-035), thuần hàm nên unit test được không cần DB.
///
/// <para>
/// 🔴 <b>Vì sao mọi thứ nằm ở đây chứ không ở FluentValidation:</b> <c>ValidationFilter</c>
/// duyệt <c>context.ActionArguments.Values</c> rồi tra <c>IValidator&lt;kiểu-tham-số&gt;</c>.
/// Với action multipart thì tham số là <c>IFormFile</c>, và không có validator nào đăng ký
/// cho kiểu đó — nên <b>đường validate tự động KHÔNG BAO GIỜ chạy cho upload</b>. Thiết kế
/// dựa vào nó là để ngỏ toàn bộ cửa.
/// </para>
///
/// <para>
/// Thứ tự các bước có chủ đích: rẻ trước, đắt sau, và bước đọc byte đầu file (bước đắt
/// nhất) chỉ chạy khi mọi kiểm tra trên metadata đã qua.
/// </para>
/// </summary>
public static class AttachmentContentValidator
{
    /// <summary>
    /// Phần mở rộng KHÔNG BAO GIỜ được xuất hiện, kể cả ở giữa tên (<c>a.php.png</c>).
    /// <para>
    /// Vì sao chặn cả đuôi ở giữa: một số máy chủ web (Apache với <c>mod_mime</c> cấu hình
    /// lỏng) chọn handler theo phần mở rộng <b>bất kỳ</b> mà chúng nhận ra, không chỉ phần
    /// cuối — nên <c>shell.php.png</c> có thể được thực thi như PHP. Ta không phục vụ file
    /// tĩnh nên rủi ro thấp, nhưng đây là loại giả định dễ thay đổi khi đổi hạ tầng.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "exe", "dll", "bat", "cmd", "com", "scr", "msi", "sh", "bash", "ps1", "psm1",
        "js", "mjs", "vbs", "jar", "php", "phtml", "asp", "aspx", "jsp", "jspx",
        "cgi", "pl", "py", "rb", "htaccess", "html", "htm", "svg", "xhtml"
    };

    /// <summary>
    /// Chữ ký (magic number) của các định dạng được phép. Định dạng nào KHÔNG có mặt ở đây
    /// là định dạng không có chữ ký ổn định — xem <see cref="SignatureOptional"/>.
    /// </summary>
    private static readonly Dictionary<string, byte[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"]  = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
        [".jpg"]  = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
        [".gif"]  = [[0x47, 0x49, 0x46, 0x38]],                       // GIF8
        [".webp"] = [[0x52, 0x49, 0x46, 0x46]],                       // RIFF (WEBP ở byte 8-11)
        [".pdf"]  = [[0x25, 0x50, 0x44, 0x46]],                       // %PDF
        // OOXML thực chất là ZIP; định dạng Office cũ là OLE compound file.
        [".docx"] = [[0x50, 0x4B, 0x03, 0x04]],
        [".xlsx"] = [[0x50, 0x4B, 0x03, 0x04]],
        [".pptx"] = [[0x50, 0x4B, 0x03, 0x04]],
        [".zip"]  = [[0x50, 0x4B, 0x03, 0x04], [0x50, 0x4B, 0x05, 0x06], [0x50, 0x4B, 0x07, 0x08]],
        [".doc"]  = [[0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]],
        [".xls"]  = [[0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]],
        [".ppt"]  = [[0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]]
    };

    /// <summary>
    /// 🔴 Ghi tường minh chứ không để thành lỗ hổng im lặng: <c>.txt</c> và <c>.csv</c> là
    /// văn bản thuần, <b>không có chữ ký</b> nào để kiểm. Chấp nhận có ý thức — chúng cũng
    /// là hai định dạng không có khả năng thực thi, và endpoint tải về luôn trả
    /// <c>application/octet-stream</c> kèm <c>nosniff</c> nên trình duyệt không diễn giải
    /// nội dung.
    /// </summary>
    private static readonly HashSet<string> SignatureOptional =
        new(StringComparer.OrdinalIgnoreCase) { ".txt", ".csv" };

    public const int MaxFileNameLength = 255;

    /// <summary>
    /// Kiểm metadata. Trả về phần mở rộng đã chuẩn hóa (viết thường, có dấu chấm).
    /// KHÔNG đụng tới nội dung — gọi <see cref="ValidateSignature"/> sau.
    /// </summary>
    public static string ValidateMetadata(
        string fileName, string contentType, long sizeBytes, IAttachmentPolicy policy)
    {
        // (2) Rỗng
        if (sizeBytes <= 0)
            throw new BusinessRuleException("File rỗng — không có gì để tải lên.");

        // (3) Quá lớn -> 413, có hành động khắc phục rõ ràng cho người dùng
        if (sizeBytes > policy.MaxFileBytes)
            throw new PayloadTooLargeException(
                $"File vượt quá giới hạn {policy.MaxFileBytes / (1024 * 1024)} MB.");

        // (4) Tên file. Path.GetFileName cắt mọi thành phần thư mục, nên nếu kết quả KHÁC
        //     chuỗi ban đầu thì tên đã chứa dấu phân cách -> đầu vào có ý đồ.
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Length > MaxFileNameLength
            || Path.GetFileName(fileName) != fileName
            || fileName.StartsWith('.')
            || fileName.IndexOfAny(['/', '\\', ':', '\0']) >= 0
            || fileName.Contains(".."))
        {
            throw new BusinessRuleException("Tên file không hợp lệ.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
            throw new BusinessRuleException("File phải có phần mở rộng.");

        // (5) Đuôi kép: chỉ kiểm các đoạn Ở GIỮA — bỏ đoạn đầu (tên gốc) và đoạn cuối
        //     (phần mở rộng thật, đã có whitelist ở bước (6) lo).
        //     Ranh giới này quan trọng cho MÃ LỖI, không chỉ cho sự gọn gàng:
        //       • "script.exe"     -> không có đoạn giữa -> rơi xuống (6) -> 415
        //         "định dạng không được hỗ trợ" là câu trả lời đúng cho một file exe thật.
        //       • "shell.php.png"  -> đoạn giữa "php"    -> 400
        //         đây là tên file có ý đồ đánh lừa, không phải một định dạng chưa hỗ trợ.
        var segments = fileName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 2)
        {
            foreach (var segment in segments[1..^1])
                if (DangerousExtensions.Contains(segment))
                    throw new BusinessRuleException(
                        $"Tên file chứa phần mở rộng bị cấm ('.{segment}').");
        }

        // (6) Whitelist đuôi -> 415
        if (!policy.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new UnsupportedMediaTypeException(
                $"Định dạng '{extension}' không được hỗ trợ. Cho phép: " +
                string.Join(", ", policy.AllowedExtensions));

        // (7) Whitelist content-type
        if (string.IsNullOrWhiteSpace(contentType)
            || !policy.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnsupportedMediaTypeException(
                $"Kiểu nội dung '{contentType}' không được hỗ trợ.");
        }

        return extension;
    }

    /// <summary>
    /// (8) Đối chiếu byte đầu file với chữ ký của định dạng.
    /// <para>
    /// Đây là bước duy nhất nhìn vào NỘI DUNG. Không có nó thì đổi tên <c>evil.exe</c> thành
    /// <c>evil.png</c> và khai <c>Content-Type: image/png</c> là qua sạch mọi kiểm tra phía
    /// trên — cả đuôi lẫn content-type đều do client tự khai.
    /// </para>
    /// <para>
    /// Trả về 400 chứ không 415: file <b>nói dối</b> về định dạng của nó là đầu vào sai lệch,
    /// khác với một định dạng hợp lệ mà hệ thống chưa hỗ trợ.
    /// </para>
    /// </summary>
    /// <param name="header">8 byte đầu (hoặc ít hơn nếu file ngắn).</param>
    public static void ValidateSignature(ReadOnlySpan<byte> header, string extension)
    {
        if (SignatureOptional.Contains(extension)) return;

        if (!Signatures.TryGetValue(extension, out var accepted))
            throw new UnsupportedMediaTypeException(
                $"Định dạng '{extension}' chưa có quy tắc kiểm chữ ký.");

        foreach (var signature in accepted)
        {
            if (header.Length < signature.Length) continue;
            if (header[..signature.Length].SequenceEqual(signature)) return;
        }

        throw new BusinessRuleException(
            $"Nội dung file không khớp với định dạng '{extension}' đã khai. " +
            "File có thể đã bị đổi tên phần mở rộng.");
    }

    /// <summary>Số byte cần đọc để kiểm chữ ký — chữ ký dài nhất đang dùng là 8 byte.</summary>
    public const int SignatureBufferSize = 8;
}
