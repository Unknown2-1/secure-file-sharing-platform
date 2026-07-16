using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VaultShare.Application.Shares;

namespace VaultShare.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/public/shares")]
public sealed class PublicSharesController(IShareService shareService) : ControllerBase
{
    [EnableRateLimiting("public-share-access")]
    [HttpPost("access")]
    public async Task<ActionResult<PublicShareSession>> Access(PublicShareAccessRequest request,
        CancellationToken cancellationToken)
    {
        var result = await shareService.CreatePublicSessionAsync(request.PublicIdentifier,
            request.SecretToken, request.Password, cancellationToken);
        if (result.Status != ShareOperationStatus.Success || result.Value is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Tautan atau kredensial tidak dapat digunakan.",
                extensions: new Dictionary<string, object?> { ["code"] = "share.access_denied" });
        var session = result.Value;
        Response.Cookies.Append("VaultShare.ShareSession", session.SessionToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1",
            Expires = session.ExpiresAt,
            IsEssential = true,
        });
        return Ok(new { session.ExpiresAt, session.Name, session.Description, session.AllowPreview, session.Files });
    }

    public sealed record PublicShareAccessRequest(
        [param: Required, StringLength(32)] string PublicIdentifier,
        [param: Required, StringLength(128)] string SecretToken,
        [param: StringLength(128)] string? Password);
}
