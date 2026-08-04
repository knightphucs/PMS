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

    public async Task<PagedResult<ProjectWithRole>> GetPagedForEmployeeAsync(
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

        var ordered = (request.SortBy?.ToLowerInvariant(), request.IsDescending) switch
        {
            ("name", false)   => query.OrderBy(p => p.Name),
            ("name", true)    => query.OrderByDescending(p => p.Name),
            ("status", false) => query.OrderBy(p => p.Status),
            ("status", true)  => query.OrderByDescending(p => p.Status),
            (_, true)         => query.OrderByDescending(p => p.ExpectedCompletionDate),
            _                 => query.OrderBy(p => p.ExpectedCompletionDate)
        };

        // Tie-break theo Id: thiếu nó thì hai bản ghi có cùng khóa sắp xếp (cùng tên, cùng
        // CreatedAt, cùng trạng thái) có thứ tự KHÔNG xác định giữa hai truy vấn, nên phân
        // trang có thể trả trùng một dòng ở trang này và bỏ sót nó ở trang kia. Lỗi chỉ lộ
        // khi dữ liệu đủ nhiều và đúng lúc — tức là ở production chứ không phải lúc dev.
        query = ordered.ThenBy(p => p.Id);

        // Lấy kèm vai trò của chính employee trong từng project. `First` an toàn vì mệnh
        // đề Where phía trên đã bảo đảm có đúng một ProjectMember Accepted khớp
        // (unique index trên (ProjectId, EmployeeId) là chốt chặn cuối — ADR-012).
        var items = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(p => new ProjectWithRole(
                p,
                p.Members.First(m => m.EmployeeId == employeeId
                                  && m.InvitationStatus == InvitationStatus.Accepted).RoleInProject))
            .ToListAsync(ct);

        return new PagedResult<ProjectWithRole>
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

    // IgnoreQueryFilters() là cố ý: unique index trên Projects.Key KHÔNG lọc theo IsDeleted,
    // nên kiểm trùng mà bỏ sót project đã xóa mềm sẽ sinh ra một mã "trống" trên giấy tờ
    // rồi vỡ ở tầng DB khi insert.
    public async Task<bool> KeyExistsAsync(string key, CancellationToken ct = default)
        => await DbSet
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Key == key, ct);

    public async Task<string?> GetKeyAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => p.Key)
            .FirstOrDefaultAsync(ct);
}
