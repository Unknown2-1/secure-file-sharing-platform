using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VaultShare.Application.Workspaces;

namespace VaultShare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/workspace-invitations")]
public sealed class WorkspaceInvitationsController(IWorkspaceService workspaceService) : ControllerBase
{
    [EnableRateLimiting("workspace-invitation")]
    [HttpPost("accept")]
    public async Task<IActionResult> Accept(
        AcceptInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await workspaceService.AcceptInvitationAsync(
            request.InvitationId,
            request.SecretToken,
            User,
            cancellationToken);
        return result.Status switch
        {
            WorkspaceOperationStatus.Success => NoContent(),
            WorkspaceOperationStatus.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Undangan tidak dapat diterima.",
                extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode }),
            _ => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Undangan tidak valid atau sudah berakhir.",
                extensions: new Dictionary<string, object?> { ["code"] = "workspace.invitation_invalid" }),
        };
    }

    public sealed record AcceptInvitationRequest(
        [param: Required] Guid InvitationId,
        [param: Required, StringLength(128, MinimumLength = 32)] string SecretToken);
}
