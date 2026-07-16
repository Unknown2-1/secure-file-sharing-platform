using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VaultShare.Application.Authentication;

namespace VaultShare.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IAccountService accountService,
    IAntiforgery antiforgery,
    IWebHostEnvironment environment) : ControllerBase
{
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpGet("csrf")]
    public ActionResult<CsrfResponse> Csrf()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken ?? string.Empty, new CookieOptions
        {
            HttpOnly = false,
            Secure = environment.IsProduction(),
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
        });
        return Ok(new CsrfResponse(tokens.RequestToken ?? string.Empty));
    }

    [AllowAnonymous]
    [EnableRateLimiting("register")]
    [HttpPost("register")]
    public async Task<ActionResult<AccountProfile>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.RegisterAsync(
            new RegisterAccountCommand(request.Email, request.Password, request.DisplayName),
            cancellationToken);

        return result.Succeeded && result.Value is not null
            ? Created("/api/v1/auth/me", result.Value)
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Registrasi tidak dapat diproses.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ActionResult<AccountProfile>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.LoginAsync(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);

        if (result.Succeeded && result.Value is not null)
        {
            return Ok(result.Value);
        }

        return result.ErrorCode == "account.two_factor_required"
            ? Accepted(new { code = result.ErrorCode })
            : Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Email atau password tidak valid.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login/two-factor")]
    public async Task<ActionResult<AccountProfile>> TwoFactorLogin(
        TwoFactorLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.CompleteTwoFactorLoginAsync(request.Code, request.RecoveryCode, cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Ok(result.Value)
            : Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Kode autentikasi tidak valid.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }

    [AllowAnonymous]
    [EnableRateLimiting("account-recovery")]
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.VerifyEmailAsync(request.Email, request.Token, cancellationToken);
        return result.Succeeded
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Verifikasi email tidak dapat diproses.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }

    [AllowAnonymous]
    [EnableRateLimiting("account-recovery")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await accountService.RequestPasswordResetAsync(request.Email, cancellationToken);
        return Accepted(new { code = "account.password_reset_if_eligible" });
    }

    [AllowAnonymous]
    [EnableRateLimiting("account-recovery")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.ResetPasswordAsync(
            request.Email,
            request.Token,
            request.NewPassword,
            cancellationToken);
        return result.Succeeded
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Reset password tidak dapat diproses.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }

    [AllowAnonymous]
    [EnableRateLimiting("account-recovery")]
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await accountService.ResendEmailVerificationAsync(request.Email, cancellationToken);
        return Accepted(new { code = "account.verification_if_eligible" });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AccountProfile>> Me(CancellationToken cancellationToken)
    {
        var profile = await accountService.GetCurrentAsync(User, cancellationToken);
        return profile is null ? Unauthorized() : Ok(profile);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await accountService.LogoutAsync(User, cancellationToken);
        return NoContent();
    }

    public sealed record RegisterRequest(
        [param: Required, EmailAddress, StringLength(254)] string Email,
        [param: Required, StringLength(128, MinimumLength = 12)] string Password,
        [param: Required, StringLength(120, MinimumLength = 2)] string DisplayName);

    public sealed record LoginRequest(
        [param: Required, EmailAddress, StringLength(254)] string Email,
        [param: Required, StringLength(128)] string Password);

    public sealed record VerifyEmailRequest(
        [param: Required, EmailAddress, StringLength(254)] string Email,
        [param: Required, StringLength(4096)] string Token);

    public sealed record ForgotPasswordRequest(
        [param: Required, EmailAddress, StringLength(254)] string Email);

    public sealed record ResetPasswordRequest(
        [param: Required, EmailAddress, StringLength(254)] string Email,
        [param: Required, StringLength(4096)] string Token,
        [param: Required, StringLength(128, MinimumLength = 12)] string NewPassword);

    public sealed record TwoFactorLoginRequest(
        [param: StringLength(16)] string? Code,
        [param: StringLength(64)] string? RecoveryCode);

    public sealed record CsrfResponse(string RequestToken);
}
