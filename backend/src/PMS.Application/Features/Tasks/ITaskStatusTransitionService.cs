using PMS.Domain.Enums;

namespace PMS.Application.Features.Tasks;

public record ChangeTaskStatusRequest(Status Target);

public interface ITaskStatusTransitionService
{
    Task<TaskSummaryResponse> ChangeStatusAsync(
        Guid taskId, ChangeTaskStatusRequest request, CancellationToken ct = default);
}
