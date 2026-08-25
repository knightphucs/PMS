using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Models;
using PMS.Application.Features.RequestPortal;

namespace PMS.API.Controllers;

/// <summary>
/// Cổng yêu cầu (ADR-063) — bề mặt của <b>người gửi</b>, tách hẳn khỏi bề mặt làm việc.
///
/// <para>
/// 🔴 <b>Không một action nào ở đây nằm dưới <c>/projects/{id}/…</c>, và đó là chủ đích.</b>
/// Route project-scoped mang theo một lời hứa ngầm: "bạn là thành viên project này". Người
/// dùng cổng yêu cầu thì không, nên đặt endpoint của họ dưới cùng tiền tố sẽ khiến phiên sau
/// gắn <c>_authz.AuthorizeAsync</c> vào cho "nhất quán" — và phá đúng thứ ADR-063 dựng lên.
/// </para>
/// <para>
/// Ba vai, ba bề mặt (luật 5 Doctrine §0): <b>người gửi</b> thấy controller này ·
/// <b>người xử lý</b> thấy hàng đợi ở màn Danh sách (một <c>SavedView</c>, ADR-061) ·
/// <b>người cấu hình</b> thấy <c>/projects/{id}/settings</c>.
/// </para>
/// <para>
/// ⚠️ <c>[Authorize]</c> trần — chỉ cần đã đăng nhập, không policy quyền hệ thống nào. Toàn
/// bộ phân quyền nằm trong vị từ truy vấn của <see cref="RequestPortalService"/>.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/request-portal")]
[Authorize]
public class RequestPortalController : ControllerBase
{
    private readonly IRequestPortalService _service;

    public RequestPortalController(IRequestPortalService service) => _service = service;

    /// <summary>Các project đang mở cổng tiếp nhận. Rỗng = chưa đội nào bật cổng.</summary>
    [HttpGet("projects")]
    [ProducesResponseType(typeof(IReadOnlyList<RequestPortalProjectResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RequestPortalProjectResponse>>> ListPortals(
        CancellationToken ct)
        => Ok(await _service.ListPortalsAsync(ct));

    /// <summary>
    /// Lược đồ form của một project.
    /// <para>404 khi project không tồn tại, đã xoá, <b>hoặc</b> chưa mở cổng — cùng một câu
    /// trả lời cho cả ba, đúng khuôn ADR-019.</para>
    /// </summary>
    [HttpGet("projects/{projectId:guid}/form")]
    [ProducesResponseType(typeof(RequestPortalFormResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestPortalFormResponse>> GetForm(
        Guid projectId, CancellationToken ct)
        => Ok(await _service.GetFormAsync(projectId, ct));

    /// <summary>
    /// Gửi một yêu cầu.
    /// <para>
    /// <b>400</b> khi thiếu trường bắt buộc của loại (guard G2 — điểm cưỡng chế
    /// <c>IsRequired</c> lúc tạo, thứ <c>POST /tasks</c> cố ý KHÔNG có).
    /// <b>404</b> khi loại không tồn tại, thuộc project khác, hoặc không
    /// <c>IsRequestable</c> (guard G1).
    /// </para>
    /// </summary>
    [HttpPost("projects/{projectId:guid}/requests")]
    [ProducesResponseType(typeof(MyRequestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyRequestResponse>> Submit(
        Guid projectId, [FromBody] SubmitRequestRequest request, CancellationToken ct)
    {
        var created = await _service.SubmitAsync(projectId, request, ct);
        return CreatedAtAction(nameof(GetMyRequest), new { taskId = created.TaskId }, created);
    }

    /// <summary>Yêu cầu do CHÍNH người gọi gửi, xuyên dự án, mới nhất trước.</summary>
    [HttpGet("requests")]
    [ProducesResponseType(typeof(PagedResult<MyRequestResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MyRequestResponse>>> ListMyRequests(
        [FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _service.ListMyRequestsAsync(request, ct));

    /// <summary>
    /// Chi tiết một yêu cầu của chính mình.
    /// <para>⚠️ <b>404 chứ không 403</b> khi yêu cầu thuộc người khác (guard G3) — 403 sẽ
    /// xác nhận rằng id đó có tồn tại.</para>
    /// </summary>
    [HttpGet("requests/{taskId:guid}")]
    [ProducesResponseType(typeof(MyRequestDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyRequestDetailResponse>> GetMyRequest(
        Guid taskId, CancellationToken ct)
        => Ok(await _service.GetMyRequestAsync(taskId, ct));
}
