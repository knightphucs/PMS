using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Features.Employees;

namespace PMS.API.Controllers;

/// <summary>
/// Tra nhân viên cho ô gợi ý khi mời thành viên — mở cho **mọi người dùng đã đăng nhập**.
///
/// <para>
/// 🔴 Đây KHÔNG phải phiên bản không-cần-quyền của <c>AdminEmployeesController</c>. Nó trả
/// đúng ba trường (<c>id</c>, <c>name</c>, <c>email</c>), bắt buộc từ khóa ≥ 2 ký tự và có
/// trần kết quả cứng ở server. Bỏ bất kỳ ràng buộc nào trong ba cái đó là biến nó thành API
/// dump danh bạ công ty — xem <c>EmployeeLookupService</c>.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/employees")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeLookupService _lookup;

    public EmployeesController(IEmployeeLookupService lookup) => _lookup = lookup;

    /// <summary>Tra nhân viên theo tên hoặc email.</summary>
    /// <response code="400">Từ khóa ngắn hơn 2 ký tự.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeLookupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<EmployeeLookupResponse>>> Search(
        [FromQuery] string? search, CancellationToken ct)
        => Ok(await _lookup.SearchAsync(search, ct));
}
