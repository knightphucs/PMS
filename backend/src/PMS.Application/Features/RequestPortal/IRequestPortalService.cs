using PMS.Application.Common.Models;

namespace PMS.Application.Features.RequestPortal;

public interface IRequestPortalService
{
    Task<IReadOnlyList<RequestPortalProjectResponse>> ListPortalsAsync(CancellationToken ct = default);

    Task<RequestPortalFormResponse> GetFormAsync(Guid projectId, CancellationToken ct = default);

    Task<MyRequestResponse> SubmitAsync(
        Guid projectId, SubmitRequestRequest request, CancellationToken ct = default);

    Task<PagedResult<MyRequestResponse>> ListMyRequestsAsync(
        PagedRequest request, CancellationToken ct = default);

    Task<MyRequestDetailResponse> GetMyRequestAsync(Guid taskId, CancellationToken ct = default);
}
