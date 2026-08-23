namespace PMS.Application.Features.Approvals;

public interface IApprovalService
{
    // ---------- luật duyệt ----------

    Task<IReadOnlyList<ApprovalPolicyResponse>> ListPoliciesAsync(
        Guid projectId, CancellationToken ct = default);

    Task<ApprovalPolicyResponse> CreatePolicyAsync(
        Guid projectId, CreateApprovalPolicyRequest request, CancellationToken ct = default);

    Task<ApprovalPolicyResponse> UpdatePolicyAsync(
        Guid id, UpdateApprovalPolicyRequest request, CancellationToken ct = default);

    Task DeletePolicyAsync(Guid id, CancellationToken ct = default);

    // ---------- yêu cầu duyệt ----------

    Task<TaskApprovalsResponse> GetForTaskAsync(Guid taskId, CancellationToken ct = default);

    Task<ApprovalResponse> DecideAsync(
        Guid approvalId, CreateApprovalDecisionRequest request, CancellationToken ct = default);

    Task<ApprovalResponse> CancelAsync(Guid approvalId, CancellationToken ct = default);
}
