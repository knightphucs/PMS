namespace PMS.Application.Common.Interfaces;

/// <summary>
/// Lưu trữ file nhị phân (ADR-035).
/// <para>
/// 🔴 Chú ý <b>hình dạng</b> của <see cref="SaveAsync"/>: người gọi truyền phần mở rộng,
/// <b>không</b> truyền tên file hay đường dẫn. Tên trên đĩa do implementation tự sinh
/// (<c>{guid}{ext}</c>). Nghĩa là path traversal <b>bất khả thi về cấu trúc</b> — không có
/// tham số nào để nhét <c>../</c> vào — chứ không phải nhờ nhớ validate ở mọi call site.
/// Cùng nguyên tắc "bảo đảm bằng cấu trúc hơn bằng kỷ luật lập trình viên" mà ADR-023 dùng
/// cho chữ ký <c>INotificationRepository</c> và ADR-008 dùng cho <c>ISoftDeletable</c>.
/// </para>
/// </summary>
public interface IFileStorage
{
    /// <returns>Tên file đã lưu (<c>StoredFileName</c>) — giá trị duy nhất cần cất vào DB.</returns>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string storedFileName, CancellationToken ct = default);

    /// <summary>Xóa file. Không ném nếu file đã biến mất — xóa là thao tác idempotent.</summary>
    Task DeleteAsync(string storedFileName, CancellationToken ct = default);
}
