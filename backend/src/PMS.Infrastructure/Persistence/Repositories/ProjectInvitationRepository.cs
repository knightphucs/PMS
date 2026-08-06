using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;

namespace PMS.Infrastructure.Persistence.Repositories;

public class ProjectInvitationRepository
    : Repository<ProjectInvitation>, IProjectInvitationRepository
{
    public ProjectInvitationRepository(PmsDbContext context) : base(context) { }

    public async Task<ProjectInvitation?> GetByHashAsync(
        string tokenHash, CancellationToken ct = default)
        => await DbSet
            .Include(i => i.Project)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);

    public async Task<ProjectInvitation?> GetPendingByProjectAndEmailAsync(
        Guid projectId, string email, CancellationToken ct = default)
        => await DbSet
            .Where(i => i.ProjectId == projectId
                     && i.Email.ToLower() == email.ToLower()
                     && i.UsedAt == null
                     && i.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync(ct);
}
