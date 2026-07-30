using PMS.Application.Common.Models;
using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IEmployeeRepository : IRepository<Employee>
{    
    Task<Employee?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<PagedResult<Employee>> GetPagedAsync(PagedRequest request, CancellationToken ct = default);

    /// <summary>Số SystemAdmin chưa bị khóa, không tính người đang bị thao tác.</summary>
    Task<int> CountActiveAdminsExceptAsync(Guid excludingId, CancellationToken ct = default);
}