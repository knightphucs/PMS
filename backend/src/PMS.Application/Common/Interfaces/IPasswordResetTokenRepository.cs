using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IPasswordResetTokenRepository : IRepository<PasswordResetToken>
{
    /// <summary>
    /// Tra theo hash. Trả về <c>null</c> khi không tồn tại — service KHÔNG được phân biệt
    /// trường hợp này với "hết hạn"/"đã dùng", cả ba phải ra cùng một lỗi (ADR-041).
    /// </summary>
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Token còn dùng được của một người — để vô hiệu hết khi cấp mới hoặc khi đổi mật khẩu xong.</summary>
    Task<IReadOnlyList<PasswordResetToken>> GetUsableByEmployeeAsync(
        Guid employeeId, CancellationToken ct = default);
}
