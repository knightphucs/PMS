using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Models;
using PMS.Application.Features.ActivityLogs;

namespace PMS.API.Controllers;

/// <summary>
/// Nhật ký CẤP HỆ THỐNG — đối trọng của quyết định "SystemAdmin không có đặc quyền nghiệp
/// vụ nào" (ADR-042). SystemAdmin cần trách nhiệm giải trình cho những việc họ thật sự làm
/// (khóa tài khoản, cấp quyền, sửa nhãn toàn cục), và đây là chỗ đó — <b>không phải</b> một
/// cửa sau để đọc hoạt động của project.
/// <para>
/// 🔴 Cố ý KHÔNG có tham số <c>entityType</c>: danh sách loại đối tượng được đọc do
/// <c>ActivityLogService</c> hard-code. Nhận nó từ query param là biến endpoint này thành
/// kênh đọc vào activity của mọi project và task.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/audit-logs")]
[Authorize(Policy = "require-system-admin")]
public class AdminAuditController : ControllerBase
{
    private readonly IActivityLogService _activity;

    public AdminAuditController(IActivityLogService activity) => _activity = activity;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SystemAuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<SystemAuditLogResponse>>> GetAll(
        [FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _activity.GetSystemAuditAsync(request, ct));
}
