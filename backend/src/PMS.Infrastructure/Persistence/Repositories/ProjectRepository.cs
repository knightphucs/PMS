using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Infrastructure.Persistence.Repositories;

public class ProjectRepository : Repository<Project>, IProjectRepository
{
    public ProjectRepository(PmsDbContext context) : base(context) { }

    public async Task<Project?> GetWithMembersAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(p => p.Members)
                .ThenInclude(m => m.Employee)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<PagedResult<Project>> GetPagedForEmployeeAsync(
        Guid employeeId, PagedRequest request, CancellationToken ct = default)
    {
        // Global Query Filter đã tự loại project IsDeleted = true.
        var query = DbSet
            .AsNoTracking()
            .Where(p => p.Members.Any(m => m.EmployeeId == employeeId
                                        && m.InvitationStatus == InvitationStatus.Accepted));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim();
            query = query.Where(p => p.Name.Contains(keyword));
        }

        var totalCount = await query.CountAsync(ct);

        query = (request.SortBy?.ToLowerInvariant(), request.IsDescending) switch
        {
            ("name", false)   => query.OrderBy(p => p.Name),
            ("name", true)    => query.OrderByDescending(p => p.Name),
            ("status", false) => query.OrderBy(p => p.Status),
            ("status", true)  => query.OrderByDescending(p => p.Status),
            (_, true)         => query.OrderByDescending(p => p.ExpectedCompletionDate),
            _                 => query.OrderBy(p => p.ExpectedCompletionDate)
        };

        var items = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Project>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<RoleInProject?> GetRoleInProjectAsync(Guid projectId, Guid employeeId, CancellationToken ct = default)
        => await DbSet
            .Where(p => p.Id == projectId)
            .SelectMany(p => p.Members)
            .Where(m => m.EmployeeId == employeeId && m.InvitationStatus == InvitationStatus.Accepted)
            .Select(m => (RoleInProject?)m.RoleInProject)
            .FirstOrDefaultAsync(ct);

    public async Task<Project?> GetForDeletionAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(p => p.Tasks)
            .Include(p => p.Sprints)
            .AsSplitQuery()      // 2 collection Include -> tránh cartesian explosion
            .FirstOrDefaultAsync(p => p.Id == id, ct);
}
