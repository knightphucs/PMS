using PMS.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.Watchers;

[Mapper]
public partial class WatcherMapper
{
#pragma warning disable RMG020 // Source member is not mapped to any target member
    [MapProperty("Employee.Name", nameof(WatcherResponse.EmployeeName))]
    public partial WatcherResponse ToResponse(Watcher watcher);
#pragma warning restore RMG020
}
