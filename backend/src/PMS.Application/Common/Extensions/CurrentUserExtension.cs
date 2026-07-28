using PMS.Application.Common.Exceptions;
using PMS.Application.Common.Interfaces;

namespace PMS.Application.Common.Extensions;

public static class CurrentUserExtensions
{
    public static Guid RequireEmployeeId(this ICurrentUserService currentUser)
        => currentUser.EmployeeId
           ?? throw new UnauthorizedException("Yêu cầu chưa được xác thực.");
}