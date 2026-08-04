using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Application.Common.Interfaces;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveByEmployeeAsync(Guid employeeId, CancellationToken ct = default);

    /// <summary>
    /// Mọi refresh token còn hiệu lực của MỌI người đang mang vai trò này.
    /// <para>
    /// Dùng khi tập quyền của một vai trò thay đổi (ADR-045): quyền nằm trong JWT nên token
    /// cũ vẫn mang tập quyền cũ. Thu hồi refresh token kéo cửa sổ rủi ro xuống còn đúng tuổi
    /// thọ access token (15 phút) — cùng cách xử lý và cùng lý do với
    /// <c>EmployeeAdminService.ChangeSystemRoleAsync</c> (ADR-015).
    /// </para>
    /// </summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveByRoleAsync(SystemRole role, CancellationToken ct = default);
}