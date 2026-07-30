using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Models;
using PMS.Application.Features.Admin;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/v1/admin/employees")]
[Authorize(Policy = "require-system-admin")]
public class AdminEmployeesController : ControllerBase
{
    private readonly IEmployeeAdminService _service;
    public AdminEmployeesController(IEmployeeAdminService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EmployeeAdminResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EmployeeAdminResponse>>> GetAll(
        [FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _service.GetPagedAsync(request, ct));

    [HttpPost("{id:guid}/lock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Lock(
        Guid id, [FromBody] LockAccountRequest request, CancellationToken ct)
    {
        await _service.LockAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/unlock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
    {
        await _service.UnlockAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/system-role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeSystemRole(
        Guid id, [FromBody] ChangeSystemRoleRequest request, CancellationToken ct)
    {
        await _service.ChangeSystemRoleAsync(id, request, ct);
        return NoContent();
    }
}