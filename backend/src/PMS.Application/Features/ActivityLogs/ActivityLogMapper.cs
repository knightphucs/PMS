using PMS.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.ActivityLogs;

[Mapper]
public partial class ActivityLogMapper
{
#pragma warning disable RMG020 // Source member is not mapped to any target member
    [MapProperty(nameof(ActivityLog.EmployeeId), nameof(ActivityLogResponse.ActorId))]
    [MapProperty("Employee.Name", nameof(ActivityLogResponse.ActorName))]
    public partial ActivityLogResponse ToResponse(ActivityLog log);

    [MapProperty(nameof(ActivityLog.EmployeeId), nameof(SystemAuditLogResponse.ActorId))]
    [MapProperty("Employee.Name", nameof(SystemAuditLogResponse.ActorName))]
    public partial SystemAuditLogResponse ToAuditResponse(ActivityLog log);
#pragma warning restore RMG020
}
