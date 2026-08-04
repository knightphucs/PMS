using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMS.Application.Common.Interfaces;

namespace PMS.Infrastructure.Storage;

/// <summary>
/// Lưu file xuống đĩa cục bộ (ADR-035). Đổi sang S3/Azure Blob sau này chỉ là thay class
/// này trong DI — đó chính là lý do <see cref="IFileStorage"/> tồn tại thay vì gọi thẳng
/// <c>File.*</c> trong service.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(IOptions<FileStorageOptions> options, ILogger<LocalFileStorage> logger)
    {
        _root = Path.GetFullPath(options.Value.Root);
        _logger = logger;
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(
        Stream content, string extension, CancellationToken ct = default)
    {
        // Tên do ĐÂY sinh, không phải do người dùng cung cấp. Đó là điểm mấu chốt: tên gốc
        // của người dùng chỉ được cất vào cột FileName để hiển thị, không bao giờ chạm tới
        // hệ thống file.
        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = ResolvePath(storedFileName);

        await using var target = new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);

        await content.CopyToAsync(target, ct);

        return storedFileName;
    }

    public Task<Stream> OpenReadAsync(string storedFileName, CancellationToken ct = default)
    {
        var path = ResolvePath(storedFileName);

        if (!File.Exists(path))
            throw new FileNotFoundException("Không tìm thấy file đính kèm trên hệ thống lưu trữ.", path);

        Stream stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storedFileName, CancellationToken ct = default)
    {
        var path = ResolvePath(storedFileName);

        // Idempotent: file đã biến mất (dọn tay, khôi phục backup) không phải lỗi cần ném
        // vào mặt người dùng — hàng trong DB mới là nguồn sự thật.
        if (File.Exists(path)) File.Delete(path);
        else _logger.LogWarning("Xóa file không tồn tại trên đĩa: {StoredFileName}", storedFileName);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Phòng thủ chiều sâu. Tên vốn đã do <see cref="SaveAsync"/> sinh nên không thể chứa
    /// <c>../</c>, nhưng lúc ĐỌC thì tên đến từ cột <c>StoredFileName</c> trong DB — và
    /// "dữ liệu trong DB luôn sạch" là một giả định, không phải một bảo đảm. Kiểm containment
    /// biến giả định đó thành bảo đảm.
    /// </summary>
    private string ResolvePath(string storedFileName)
    {
        var candidate = Path.GetFullPath(Path.Combine(_root, storedFileName));

        if (!candidate.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Đường dẫn file thoát ra ngoài thư mục gốc: '{storedFileName}'.");

        return candidate;
    }
}
