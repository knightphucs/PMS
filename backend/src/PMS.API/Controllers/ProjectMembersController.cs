using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Models;
using PMS.Application.Features.Projects;

namespace PMS.API.Controllers;

[ApiController]
[Route("api/v1/projects")]
[Authorize]
public class ProjectMembersController : ControllerBase
{
    private readonly IProjectMemberService _service;

    public ProjectMembersController(IProjectMemberService service) => _service = service;

    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(PagedResult<ProjectMemberResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProjectMemberResponse>>> GetMembers(
        Guid id, CancellationToken ct)
        => Ok(await _service.GetMembersAsync(id, ct));

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(typeof(ProjectMemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectMemberResponse>> Invite(
        Guid id, [FromBody] InviteMemberRequest request, CancellationToken ct)
    {
        var member = await _service.InviteAsync(id, request, ct);
        return Created($"/api/v1/projects/{id}/members", member);
    }

    [HttpPut("{id:guid}/members/{employeeId:guid}/role")]
    [ProducesResponseType(typeof(ProjectDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectMemberResponse>> ChangeRole(
        Guid id, Guid employeeId, [FromBody] ChangeMemberRoleRequest request, CancellationToken ct)
        => Ok(await _service.ChangeRoleAsync(id, employeeId, request, ct));

    [HttpDelete("{id:guid}/members/{employeeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid employeeId, CancellationToken ct)
    {
        await _service.RemoveMemberAsync(id, employeeId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/members/me/accept")]
    [ProducesResponseType(typeof(ProjectMemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectMemberResponse>> AcceptInvitation(
        Guid id, CancellationToken ct)
        => Ok(await _service.AcceptInvitationAsync(id, ct));

    [HttpPost("{id:guid}/members/me/decline")]
    [ProducesResponseType(typeof(ProjectMemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectMemberResponse>> DeclineInvitation(
        Guid id, CancellationToken ct)
        => Ok(await _service.DeclineInvitationAsync(id, ct));

    [HttpGet("invitations")]
    [ProducesResponseType(typeof(PagedResult<ProjectMemberResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MyInvitationResponse>>> GetMyInvitations(
        CancellationToken ct)
        => Ok(await _service.GetMyInvitationsAsync(ct));
}