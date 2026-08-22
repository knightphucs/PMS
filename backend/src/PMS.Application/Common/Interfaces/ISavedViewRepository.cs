using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface ISavedViewRepository : IRepository<SavedView>
{
    /// <summary>
    /// Các view của project mà <paramref name="viewerId"/> được thấy: view CHIA SẺ của cả
    /// đội, cộng view RIÊNG của chính người đó.
    ///
    /// <para>
    /// 🔴 Lọc ngay trong truy vấn chứ không trả hết rồi lọc ở service: view riêng của người
    /// khác không được rời khỏi DB, kể cả để bị bỏ đi ở tầng trên — tên một view có thể tiết
    /// lộ thứ người ta đang theo dõi.
    /// </para>
    /// <para>
    /// Sắp theo <c>Order</c> rồi tie-break bằng <c>Id</c> — <c>Order</c> không unique, cùng
    /// lý do đã ghi ở <see cref="IBoardColumnRepository.ListByProjectAsync"/>.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<SavedView>> ListVisibleAsync(
        Guid projectId, Guid viewerId, CancellationToken ct = default);

    /// <summary>Một view kèm <c>Filters</c> + <c>Columns</c>, CÓ tracking (để sửa/xoá).</summary>
    Task<SavedView?> GetWithPartsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Xoá mọi view RIÊNG của một người trong một project, trả về số view đã xoá.
    ///
    /// <para>
    /// Gọi khi người đó bị gỡ khỏi project (ADR-061). Có tiền lệ thẳng:
    /// <c>RemoveMemberAsync</c> xoá <b>cứng</b> hàng <c>ProjectMember</c> — giữ lại một view
    /// riêng của người không còn quyền vào project là giữ một bản ghi không ai đọc được và
    /// không ai dọn được.
    /// </para>
    /// <para>
    /// ⚠️ View CHIA SẺ thì <b>giữ nguyên</b>, kể cả khi chủ sở hữu đã rời đi: nó là tài sản
    /// của cả đội. Đó cũng chính là lý do quyền ghi lên view chia sẻ không chỉ thuộc về chủ
    /// sở hữu — xem <see cref="Domain.Entities.SavedView.IsShared"/>.
    /// </para>
    /// </summary>
    Task<int> DeletePrivateViewsOfMemberAsync(
        Guid projectId, Guid employeeId, CancellationToken ct = default);
}
