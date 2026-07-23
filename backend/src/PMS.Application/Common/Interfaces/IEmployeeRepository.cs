using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IEmployeeRepository : IRepository<Employee>
{    
    Task<Employee?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
}