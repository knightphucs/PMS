using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Infrastructure.Persistence.Repositories;

public class ProjectMemberRepository : Repository<ProjectMember>, IProjectMemberRepository
{
    public ProjectMemberRepository(PmsDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Guid>> FilterActiveMemberIdsAsync(
        Guid projectId, IReadOnlyCollection<Guid> candidateIds, CancellationToken ct = default)
    {
        if (candidateIds.Count == 0) return [];

        return await DbSet
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId
                     && m.InvitationStatus == InvitationStatus.Accepted
                     && candidateIds.Contains(m.EmployeeId))
            .Select(m => m.EmployeeId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProjectMember>> GetPendingInvitationsAsync(
        Guid employeeId, CancellationToken ct = default)
        => await DbSet.AsNoTracking()
                      .Include(m => m.Project)
                      .Where(m => m.EmployeeId == employeeId
                               && m.InvitationStatus == InvitationStatus.Pending)
                      .OrderByDescending(m => m.CreatedAt)
                      .ToListAsync(ct);
}