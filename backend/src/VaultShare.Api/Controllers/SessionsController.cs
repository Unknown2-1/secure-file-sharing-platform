using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VaultShare.Application.Authentication;

namespace VaultShare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/sessions")]
public sealed class SessionsController(IAccountService accountService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SessionInfo>>> List(CancellationToken cancellationToken) =>
        Ok(await accountService.ListSessionsAsync(User, cancellationToken));

    [HttpDelete("{sessionId:guid}")]
    public async Task<IActionResult> Revoke(Guid sessionId, CancellationToken cancellationToken)
    {
        var found = await accountService.RevokeSessionAsync(sessionId, User, cancellationToken);
        return found ? NoContent() : NotFound();
    }
}
