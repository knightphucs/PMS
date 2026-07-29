using PMS.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.Admin;

[Mapper]
public partial class EmployeeAdminMapper
{
    // RMG020: Employee có PasswordHash, RefreshTokens, ProjectMemberships... không map
    // sang DTO. Đó là CHỦ Ý — DTO admin không được lộ hash mật khẩu.
#pragma warning disable RMG020 // Source member is not mapped to any target member
    public partial EmployeeAdminResponse ToAdminResponse(Employee employee);
#pragma warning restore RMG020
}