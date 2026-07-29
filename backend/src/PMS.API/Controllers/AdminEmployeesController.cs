using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Models;
using PMS.Application.Features.Admin;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/v1/admin/employees")]
[Authorize(Policy = "RequireSystemAdmin")]
public class AdminEmployeesController : ControllerBase
{
    private readonly IEmployeeAdminService _service;
    public AdminEmployeesController(IEmployeeAdminService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<PagedResult<EmployeeAdminResponse>>> GetAll(
        [FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _service.GetPagedAsync(request, ct));

    [HttpPost("{id:guid}/lock")]
    public async Task<IActionResult> Lock(
        Guid id, [FromBody] LockAccountRequest request, CancellationToken ct)
    {
        await _service.LockAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
    {
        await _service.UnlockAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/system-role")]
    public async Task<IActionResult> ChangeSystemRole(
        Guid id, [FromBody] ChangeSystemRoleRequest request, CancellationToken ct)
    {
        await _service.ChangeSystemRoleAsync(id, request, ct);
        return NoContent();
    }
}