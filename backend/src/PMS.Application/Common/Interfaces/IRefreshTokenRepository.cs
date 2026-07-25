using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveByEmployeeAsync(Guid employeeId, CancellationToken ct = default);
}