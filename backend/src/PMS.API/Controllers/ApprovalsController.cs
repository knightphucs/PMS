using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Features.Approvals;

namespace PMS.API.Controllers;

/// <summary>
/// Phê duyệt là dữ liệu (ADR-062).
///
/// <para>
/// Route chia hai nhóm giống <c>SavedViewsController</c>: <c>/projects/{id}/approval-policies*</c>
/// là KHAI BÁO luật (bề mặt cấu hình, PM), còn <c>/tasks/{id}/approvals</c> và
/// <c>/approvals/{id}/*</c> là dữ liệu chạy (bề mặt làm việc, người ký).
/// </para>
/// <para>
/// 🔴 <b>Không có endpoint nào tạo một <c>Approval</c>.</b> Yêu cầu duyệt tự sinh ở
/// <c>PUT /tasks/{id}/status</c> khi task chạm một cổng — xem
/// <c>TaskStatusTransitionService.EnsureApprovedAsync</c> và ADR-062 quyết định (a). Nếu bạn
/// đang định thêm một <c>POST /tasks/{id}/approvals</c>, đọc ADR trước.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class ApprovalsController : ControllerBase
{
    private readonly IApprovalService _service;

    public ApprovalsController(IApprovalService service) => _service = service;

    // ---------- luật duyệt (cấu hình) ----------

    /// <summary>Mọi luật duyệt của dự án. Quyền <c>View</c> — cả đội cần biết luật đang áp
    /// lên việc của họ.</summary>
    [HttpGet("projects/{projectId:guid}/approval-policies")]
    [ProducesResponseType(typeof(IReadOnlyList<ApprovalPolicyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ApprovalPolicyResponse>>> ListPolicies(
        Guid projectId, CancellationToken ct)
        => Ok(await _service.ListPoliciesAsync(projectId, ct));

    [HttpPost("projects/{projectId:guid}/approval-policies")]
    [ProducesResponseType(typeof(ApprovalPolicyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApprovalPolicyResponse>> CreatePolicy(
        Guid projectId, [FromBody] CreateApprovalPolicyRequest request, CancellationToken ct)
    {
        var created = await _service.CreatePolicyAsync(projectId, request, ct);
        return CreatedAtAction(nameof(ListPolicies), new { projectId }, created);
    }

    [HttpPut("approval-policies/{id:guid}")]
    [ProducesResponseType(typeof(ApprovalPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApprovalPolicyResponse>> UpdatePolicy(
        Guid id, [FromBody] UpdateApprovalPolicyRequest request, CancellationToken ct)
        => Ok(await _service.UpdatePolicyAsync(id, request, ct));

    [HttpDelete("approval-policies/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePolicy(Guid id, CancellationToken ct)
    {
        await _service.DeletePolicyAsync(id, ct);
        return NoContent();
    }

    // ---------- yêu cầu duyệt (dữ liệu chạy) ----------

    /// <summary>
    /// Trạng thái duyệt của một task: hàng đang chặn (nếu có) + toàn bộ lịch sử.
    /// <c>hasGate</c> nuôi việc TỰ ẨN khối duyệt ở màn chi tiết (luật 3 của Doctrine).
    /// </summary>
    [HttpGet("tasks/{taskId:guid}/approvals")]
    [ProducesResponseType(typeof(TaskApprovalsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskApprovalsResponse>> GetForTask(
        Guid taskId, CancellationToken ct)
        => Ok(await _service.GetForTaskAsync(taskId, ct));

    /// <summary>
    /// Bỏ một lá phiếu.
    /// <para>
    /// ⚠️ Quyền ở đây KHÔNG theo <c>RoleInProject</c> mà theo <c>ApproverMode</c> của luật —
    /// ngoại lệ có chủ đích của mô hình hai tầng (ADR-062). Người ngoài danh sách nhận 403
    /// dù họ là PM.
    /// </para>
    /// </summary>
    [HttpPost("approvals/{id:guid}/decisions")]
    [ProducesResponseType(typeof(ApprovalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApprovalResponse>> Decide(
        Guid id, [FromBody] CreateApprovalDecisionRequest request, CancellationToken ct)
        => Ok(await _service.DecideAsync(id, request, ct));

    /// <summary>
    /// Rút yêu cầu về — người gửi HOẶC PM. Đây là lối thoát duy nhất khỏi một yêu cầu đã bị
    /// từ chối, cố ý (ADR-062 quyết định c).
    /// </summary>
    [HttpPost("approvals/{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApprovalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApprovalResponse>> Cancel(Guid id, CancellationToken ct)
        => Ok(await _service.CancelAsync(id, ct));
}
