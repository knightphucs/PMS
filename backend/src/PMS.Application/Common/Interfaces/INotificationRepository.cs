using PMS.Application.Common.Models;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Common.Interfaces;

/// <summary>
/// Mọi method đọc đều BẮT BUỘC nhận <c>employeeId</c> — không có
/// <c>GetPagedAsync(PagedRequest)</c> trần nào để service có thể gọi rồi quên lọc theo người
/// nhận. Notification là ngoại lệ hợp lệ duy nhất của ADR-006/019 (không project-scoped nên
/// không đi qua <c>IProjectAuthorizationService</c>), nên phạm vi truy cập phải được bảo đảm
/// bằng CẤU TRÚC chữ ký thay vì bằng kỷ luật lập trình viên — đúng bài học ADR-008 (ADR-023).
/// </summary>
public interface INotificationRepository : IRepository<Notification>
{
    /// <summary>
    /// Trả về <c>null</c> khi thông báo không tồn tại HOẶC không thuộc người gọi — hai
    /// trường hợp cố ý không phân biệt được, để service trả cùng một 404 (OWASP API1:2023,
    /// cùng lý do ADR-006 chọn 404 thay 403).
    /// </summary>
    Task<Notification?> GetForRecipientAsync(
        Guid id, Guid employeeId, CancellationToken ct = default);

    /// <summary><c>isRead = null</c> nghĩa là lấy cả đã đọc và chưa đọc.</summary>
    Task<PagedResult<Notification>> GetPagedForRecipientAsync(
        Guid employeeId, bool? isRead, PagedRequest request, CancellationToken ct = default);

    Task<int> CountUnreadAsync(Guid employeeId, CancellationToken ct = default);

    /// <summary>
    /// Có tracking (khác các method đọc còn lại): dùng cho "đánh dấu tất cả đã đọc", phải đi
    /// qua ChangeTracker để interceptor đóng dấu <c>UpdatedAt</c> (ADR-024).
    /// </summary>
    Task<IReadOnlyList<Notification>> GetUnreadForRecipientAsync(
        Guid employeeId, CancellationToken ct = default);

    /// <summary>
    /// Cặp <c>(EmployeeId, RelatedEntityId)</c> đã được thông báo loại
    /// <paramref name="type"/> kể từ <paramref name="since"/> — dùng để khử trùng lặp cho
    /// background job (ADR-040).
    /// <para>
    /// ⚠️ Đây là <b>ngoại lệ có ý thức</b> của luật "mọi method đọc phải nhận employeeId" ghi
    /// ở đầu interface. Nó đọc ngang qua nhiều người nhận, nhưng KHÔNG phục vụ request nào
    /// của người dùng — chỉ background job gọi, và nó chỉ trả về cặp id, không trả nội dung
    /// thông báo. Ghi rõ ở đây để lần sau không ai tưởng luật kia đã bị nới lỏng.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<(Guid EmployeeId, Guid RelatedEntityId)>> GetNotifiedPairsSinceAsync(
        NotificationType type, DateTime since, IReadOnlyCollection<Guid> relatedEntityIds,
        CancellationToken ct = default);
}
