using PMS.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace PMS.Application.Features.Tasks;

[Mapper]
public partial class TaskMapper
{
#pragma warning disable RMG020 // Source member is not mapped to any target member
    [MapProperty(nameof(TaskItem.Assignments), nameof(TaskSummaryResponse.Assignees))]
    public partial TaskSummaryResponse ToSummary(TaskItem task);

    [MapProperty(nameof(TaskAssignment.Employee.Name), nameof(TaskCardAssignee.EmployeeName))]
    public partial TaskCardAssignee ToCardAssignee(TaskAssignment assignment);

    [MapProperty(nameof(TaskItem.Assignments), nameof(TaskDetailResponse.Assignees))]
    [MapProperty("Reporter.Name", nameof(TaskDetailResponse.ReporterName))]
    public partial TaskDetailResponse ToDetail(TaskItem task);

    [MapProperty(nameof(TaskAssignment.Employee.Name), nameof(TaskAssigneeResponse.EmployeeName))]
    public partial TaskAssigneeResponse ToAssigneeResponse(TaskAssignment assignment);
#pragma warning restore RMG020 // Source member is not mapped to any target member
}
