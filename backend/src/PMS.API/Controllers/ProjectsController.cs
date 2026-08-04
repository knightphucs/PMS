// PMS.API/Controllers/ProjectsController.cs
using Microsoft.AspNetCore.Authorization;
using PMS.Application.Common.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Models;
using PMS.Application.Features.Projects;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projects;
    public ProjectsController(IProjectService projects) => _projects = projects;

    [HttpPost]
    [Authorize(Policy = SystemPermissions.ProjectsCreate)]
    [ProducesResponseType(typeof(ProjectSummaryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectSummaryResponse>> Create(
        CreateProjectRequest req, CancellationToken ct)
    {
        var created = await _projects.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProjectSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProjectSummaryResponse>>> GetMine(
        [FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _projects.GetMineAsync(request, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _projects.GetByIdAsync(id, ct));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectDetailResponse>> Update(
        Guid id, UpdateProjectRequest req, CancellationToken ct)
        => Ok(await _projects.UpdateAsync(id, req, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _projects.DeleteAsync(id, ct);
        return NoContent();
    }
}