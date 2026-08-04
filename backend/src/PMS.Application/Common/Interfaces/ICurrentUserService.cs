namespace PMS.Application.Common.Interfaces;

/// <summary>
/// Danh tính của người gọi trong request hiện tại.
///
/// <para>
/// 🔴 <b>Cố ý KHÔNG có <c>SystemRole</c> lẫn <c>Permissions</c>.</b> Quyền tầng 1 được cưỡng
/// chế 100% ở tầng policy của ASP.NET (<c>[Authorize(Policy = SystemPermissions.*)]</c>) —
/// service không kiểm lại, và đó là chủ ý.
/// </para>
/// <para>
/// <c>SystemRole</c> từng nằm ở đây suốt nhiều phiên mà <b>không service nào đọc</b>. Sau
/// ADR-045 nó còn tệ hơn là thừa: nó gây hiểu nhầm, vì người đọc sẽ tưởng vai trò vẫn là trục
/// phân quyền trong khi trục đó nay là claim <c>permission</c>. Đã xóa 2026-08-04.
/// </para>
/// <para>
/// Cùng lý do, đừng thêm <c>Permissions</c> vào đây "cho đủ bộ": nó sẽ là một member không có
/// người đọc nào — đúng hình dạng lỗi mà <c>ValidationFilter</c> tra
/// <c>IValidator&lt;IFormFile&gt;</c> đã trả giá (một chốt an toàn TRÔNG NHƯ đã khóa mà không
/// ai gọi tới). Khi nào có service thật cần nó thì thêm, <b>cùng commit với người đọc đầu
/// tiên</b>.
/// </para>
/// </summary>
public interface ICurrentUserService
{
    Guid? EmployeeId { get; }
    string? IpAddress { get; }
    bool IsAuthenticated { get; }
}
