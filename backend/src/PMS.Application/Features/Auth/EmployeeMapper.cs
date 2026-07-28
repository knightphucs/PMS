using PMS.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.Auth;

[Mapper]
public partial class EmployeeMapper
{
#pragma warning disable RMG020 // Source member is not mapped to any target member
    public partial EmployeeDto ToDto(Employee employee);
#pragma warning restore RMG020 // Source member is not mapped to any target member
}