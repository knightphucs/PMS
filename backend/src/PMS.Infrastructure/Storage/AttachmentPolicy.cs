using Microsoft.Extensions.Options;
using PMS.Application.Features.Attachments;

namespace PMS.Infrastructure.Storage;

/// <summary>
/// Cầu nối giữa <see cref="FileStorageOptions"/> (cấu hình, thuộc Infrastructure) và
/// <see cref="IAttachmentPolicy"/> (thứ tầng Application cần biết). Tách ra để
/// <c>AttachmentContentValidator</c> không phải phụ thuộc
/// <c>Microsoft.Extensions.Options</c> — nhờ đó nó unit test được bằng một fake ba dòng.
/// </summary>
public class AttachmentPolicy : IAttachmentPolicy
{
    private readonly FileStorageOptions _options;

    public AttachmentPolicy(IOptions<FileStorageOptions> options) => _options = options.Value;

    public long MaxFileBytes => _options.MaxFileBytes;
    public IReadOnlyCollection<string> AllowedExtensions => _options.AllowedExtensions;
    public IReadOnlyCollection<string> AllowedContentTypes => _options.AllowedContentTypes;
}
