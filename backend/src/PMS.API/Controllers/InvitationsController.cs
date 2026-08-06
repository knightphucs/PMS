using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PMS.Application.Features.Projects;

namespace PMS.API.Controllers;

/// <summary>
/// Lời mời project qua email, theo token — KHÔNG đặt <c>[Authorize]</c> ở class vì hai action
/// có yêu cầu xác thực khác nhau: <see cref="Preview"/> phải public để trang landing hiện
/// được tên project TRƯỚC khi người dùng đăng nhập/đăng ký.
/// </summary>
[ApiController]
[Route("api/v1/invitations")]
public class InvitationsController : ControllerBase
{
    private readonly IProjectMemberService _service;

    public InvitationsController(IProjectMemberService service) => _service = service;

    [HttpGet("{token}"), AllowAnonymous, EnableRateLimiting("invitation-preview")]
    [ProducesResponseType(typeof(InvitationPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvitationPreviewResponse>> Preview(
        string token, CancellationToken ct)
        => Ok(await _service.GetInvitationPreviewAsync(token, ct));

    [HttpPost("{token}/accept"), Authorize, EnableRateLimiting("invitation-accept")]
    [ProducesResponseType(typeof(ProjectMemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProjectMemberResponse>> Accept(
        string token, CancellationToken ct)
        => Ok(await _service.AcceptExternalInvitationAsync(token, ct));
}
