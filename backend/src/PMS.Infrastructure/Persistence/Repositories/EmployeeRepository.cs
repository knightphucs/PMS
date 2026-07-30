using Microsoft.EntityFrameworkCore;
using PMS.Application.Common.Interfaces;
using PMS.Application.Common.Models;
using PMS.Domain.Entities;
using PMS.Domain.Enums;

namespace PMS.Infrastructure.Persistence.Repositories;

public class EmployeeRepository : Repository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(PmsDbContext context) : base(context) {}

    public async Task<int> CountActiveAdminsExceptAsync(Guid excludingId, CancellationToken ct = default)
        => await DbSet.CountAsync(
            e => e.Id != excludingId && e.SystemRole == SystemRole.SystemAdmin && !e.IsLocked
        );

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim();
        return await DbSet.AnyAsync(e => e.Email == normalized, ct);
    }

    public async Task<Employee?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await DbSet.FirstOrDefaultAsync(e => e.Email.ToLower() == normalized, ct); 
    }

    public async Task<PagedResult<Employee>> GetPagedAsync(
        PagedRequest request, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim();
            query = query.Where(e => e.Name.Contains(keyword) || e.Email.Contains(keyword));
        }

        var totalCount = await query.CountAsync(ct);

        // Whitelist bằng switch thay vì dựng OrderBy động từ chuỗi client gửi lên:
        // chuỗi tự do đi thẳng vào biểu thức sắp xếp là một dạng injection.
        query = (request.SortBy?.ToLowerInvariant(), request.IsDescending) switch
        {
            ("name",   false) => query.OrderBy(e => e.Name),
            ("name",   true)  => query.OrderByDescending(e => e.Name),
            ("email",  false) => query.OrderBy(e => e.Email),
            ("email",  true)  => query.OrderByDescending(e => e.Email),
            ("role",   false) => query.OrderBy(e => e.SystemRole),
            ("role",   true)  => query.OrderByDescending(e => e.SystemRole),
            ("locked", false) => query.OrderBy(e => e.IsLocked),
            ("locked", true)  => query.OrderByDescending(e => e.IsLocked),
            (_, true)         => query.OrderByDescending(e => e.CreatedAt),
            _                 => query.OrderBy(e => e.CreatedAt)   // mặc định: cũ nhất trước
        };

        var items = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Employee>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}