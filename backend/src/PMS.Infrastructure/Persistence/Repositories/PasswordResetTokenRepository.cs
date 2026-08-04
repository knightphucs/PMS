using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

public class PasswordResetTokenRepository
    : Repository<PasswordResetToken>, IPasswordResetTokenRepository
{
    public PasswordResetTokenRepository(PmsDbContext context) : base(context) { }

    public async Task<PasswordResetToken?> GetByHashAsync(
        string tokenHash, CancellationToken ct = default)
        => await DbSet
            .Include(t => t.Employee)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<PasswordResetToken>> GetUsableByEmployeeAsync(
        Guid employeeId, CancellationToken ct = default)
        => await DbSet
            .Where(t => t.EmployeeId == employeeId
                     && t.UsedAt == null
                     && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);
}
