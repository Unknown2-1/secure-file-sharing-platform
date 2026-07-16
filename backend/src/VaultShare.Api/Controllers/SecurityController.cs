using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VaultShare.Application.Authentication;

namespace VaultShare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/security")]
public sealed class SecurityController(IAccountService accountService) : ControllerBase
{
    [HttpGet("two-factor/setup")]
    public async Task<ActionResult<TwoFactorSetup>> Setup(CancellationToken cancellationToken) =>
        Ok(await accountService.GetTwoFactorSetupAsync(User, cancellationToken));

    [HttpPost("two-factor/enable")]
    public async Task<ActionResult<TwoFactorEnabled>> Enable(
        EnableTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.EnableTwoFactorAsync(User, request.Code, cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Ok(result.Value)
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Kode autentikasi tidak valid.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }

    [HttpPost("two-factor/recovery-codes")]
    public async Task<ActionResult<TwoFactorEnabled>> RegenerateRecoveryCodes(
        VerifyTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.RegenerateRecoveryCodesAsync(
            User,
            request.Password,
            request.Code,
            cancellationToken);
        return result.Succeeded && result.Value is not null
            ? Ok(result.Value)
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Verifikasi keamanan gagal.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }

    [HttpPost("two-factor/disable")]
    public async Task<IActionResult> Disable(
        VerifyTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.DisableTwoFactorAsync(
            User,
            request.Password,
            request.Code,
            cancellationToken);
        return result.Succeeded
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Verifikasi keamanan gagal.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.ChangePasswordAsync(
            User,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);
        return result.Succeeded
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Password tidak dapat diubah.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }

    [HttpPost("email-change/request")]
    public async Task<IActionResult> RequestEmailChange(
        RequestEmailChangeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.RequestEmailChangeAsync(User, request.NewEmail, cancellationToken);
        return result.Succeeded
            ? Accepted(new { code = "account.email_change_pending" })
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Perubahan email tidak dapat diproses.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }

    [HttpPost("email-change/confirm")]
    public async Task<IActionResult> ConfirmEmailChange(
        ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.ConfirmEmailChangeAsync(User, request.NewEmail, request.Token, cancellationToken);
        return result.Succeeded
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Perubahan email tidak dapat diproses.", extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }

    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount(
        DeleteAccountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.DeleteAccountAsync(
            User,
            request.Password,
            request.TwoFactorCode,
            request.RecoveryCode,
            cancellationToken);
        if (result.Succeeded) return NoContent();

        var status = result.ErrorCode == "account.owns_shared_workspace"
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;
        return Problem(
            statusCode: status,
            title: "Akun tidak dapat dihapus.",
            extensions: new Dictionary<string, object?> { ["code"] = result.ErrorCode });
    }

    public sealed record EnableTwoFactorRequest(
        [param: Required, StringLength(16, MinimumLength = 6)] string Code);

    public sealed record VerifyTwoFactorRequest(
        [param: Required, StringLength(128)] string Password,
        [param: Required, StringLength(16, MinimumLength = 6)] string Code);

    public sealed record ChangePasswordRequest(
        [param: Required, StringLength(128)] string CurrentPassword,
        [param: Required, StringLength(128, MinimumLength = 12)] string NewPassword);

    public sealed record RequestEmailChangeRequest(
        [param: Required, EmailAddress, StringLength(254)] string NewEmail);

    public sealed record ConfirmEmailChangeRequest(
        [param: Required, EmailAddress, StringLength(254)] string NewEmail,
        [param: Required, StringLength(4096)] string Token);

    public sealed record DeleteAccountRequest(
        [param: Required, StringLength(128)] string Password,
        [param: StringLength(16)] string? TwoFactorCode,
        [param: StringLength(64)] string? RecoveryCode);
}
