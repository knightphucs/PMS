using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : Repository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(PmsDbContext context) : base(context) { }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByEmployeeAsync(
        Guid employeeId, CancellationToken ct = default)
        => await DbSet
            .Where(rt => rt.EmployeeId == employeeId
                      && rt.RevokedAt == null
                      && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByRoleAsync(
        SystemRole role, CancellationToken ct = default)
        => await DbSet
            .Where(rt => rt.Employee.SystemRole == role
                      && rt.RevokedAt == null
                      && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);
}