using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Common.Interfaces;

/// <summary>
/// Danh mục quyền tầng 1 và ánh xạ vai trò → quyền (ADR-045). <b>Không</b> kế thừa
/// <see cref="IRepository{T}"/> vì <see cref="Permission"/> không phải <c>BaseEntity</c> —
/// tiền lệ <see cref="IProjectTaskCounterRepository"/> và <c>IWatcherRepository</c>.
/// </summary>
public interface IPermissionRepository
{
    /// <summary>
    /// Mã quyền của một vai trò. Đây là đường đọc NÓNG — gọi mỗi lần phát access token
    /// (đăng ký / đăng nhập / refresh).
    /// <para>
    /// Cố ý KHÔNG cache: kết quả là seek trên khóa clustered trả tối đa vài hàng, còn cache
    /// thì đánh đổi một vấn đề hiệu năng chưa ai đo lấy một vấn đề bảo mật vô hình — admin
    /// gỡ quyền mà cache vẫn tiếp tục phát ra nó, chồng thêm một cửa sổ trễ nữa lên trên
    /// cửa sổ 15 phút của access token (và ở nhiều instance thì việc vô hiệu hóa cache không
    /// còn cục bộ).
    /// </para>
    /// </summary>
    Task<IReadOnlyList<string>> GetCodesForRoleAsync(SystemRole role, CancellationToken ct = default);

    /// <summary>Toàn bộ danh mục (mã + mô tả) — dựng ma trận checkbox ở màn quản trị.</summary>
    Task<IReadOnlyList<Permission>> GetCatalogAsync(CancellationToken ct = default);

    /// <summary>Toàn bộ ánh xạ vai trò → quyền của mọi vai trò.</summary>
    Task<IReadOnlyList<RolePermission>> GetAllGrantsAsync(CancellationToken ct = default);

    /// <summary>
    /// Thay TOÀN BỘ tập quyền của một vai trò (ghi đè, không delta).
    /// <para>
    /// ⚠️ Đi qua ChangeTracker chứ không <c>ExecuteDelete</c>/<c>ExecuteUpdate</c> — ADR-024:
    /// bulk operation bỏ qua interceptor của <c>PmsDbContext</c>. Ở đây số hàng đếm trên đầu
    /// ngón tay nên không có gì để tối ưu.
    /// </para>
    /// <para>
    /// KHÔNG tự gọi <c>SaveChangesAsync</c> — người gọi gộp chung một lần lưu với việc thu
    /// hồi refresh token và ghi nhật ký, để ba thứ đó cùng thành công hoặc cùng thất bại.
    /// </para>
    /// </summary>
    Task ReplaceGrantsForRoleAsync(
        SystemRole role, IReadOnlyCollection<string> codes, CancellationToken ct = default);
}
