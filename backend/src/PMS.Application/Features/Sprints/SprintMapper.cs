using PMS.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.Sprints;

[Mapper]
public partial class SprintMapper
{
#pragma warning disable RMG020 // Source member is not mapped to any target member
    [MapProperty("Tasks.Count", nameof(SprintResponse.TaskCount))]
    public partial SprintResponse ToResponse(Sprint sprint);
#pragma warning restore RMG020 // Source member is not mapped to any target member
}
