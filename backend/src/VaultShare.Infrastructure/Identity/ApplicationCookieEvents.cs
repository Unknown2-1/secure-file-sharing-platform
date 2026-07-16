using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Identity;

internal sealed class ApplicationCookieEvents(
    VaultShareDbContext dbContext,
    ISecurityStampValidator securityStampValidator)
    : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        await securityStampValidator.ValidateAsync(context);
        if (context.Principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionIdValue = context.Principal?.FindFirstValue(SessionClaims.SessionId);
        if (!Guid.TryParse(userIdValue, out var userId) || !Guid.TryParse(sessionIdValue, out var sessionId))
        {
            await RejectAsync(context);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var session = await dbContext.UserSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == sessionId && candidate.UserId == userId,
            context.HttpContext.RequestAborted);

        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= now)
        {
            await RejectAsync(context);
            return;
        }

        if (session.LastSeenAt < now.AddMinutes(-5))
        {
            session.LastSeenAt = now;
            await dbContext.SaveChangesAsync(context.HttpContext.RequestAborted);
        }
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}
