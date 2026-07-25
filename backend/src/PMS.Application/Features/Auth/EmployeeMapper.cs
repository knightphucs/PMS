using PMS.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.Auth;

[Mapper]
public partial class EmployeeMapper
{
    public partial EmployeeDto ToDto(Employee employee);
}