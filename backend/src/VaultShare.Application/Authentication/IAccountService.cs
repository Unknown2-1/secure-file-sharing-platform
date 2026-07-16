using System.Security.Claims;

namespace VaultShare.Application.Authentication;

public interface IAccountService
{
    Task<AccountResult<AccountProfile>> RegisterAsync(RegisterAccountCommand command, CancellationToken cancellationToken);

    Task<AccountResult<AccountProfile>> LoginAsync(LoginCommand command, CancellationToken cancellationToken);

    Task<AccountResult<AccountProfile>> CompleteTwoFactorLoginAsync(
        string? code,
        string? recoveryCode,
        CancellationToken cancellationToken);

    Task<AccountResult<bool>> VerifyEmailAsync(string email, string token, CancellationToken cancellationToken);

    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken);

    Task<AccountResult<bool>> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken);

    Task<AccountProfile?> GetCurrentAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task LogoutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<bool> RevokeSessionAsync(Guid sessionId, ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<TwoFactorSetup> GetTwoFactorSetupAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<AccountResult<TwoFactorEnabled>> EnableTwoFactorAsync(
        ClaimsPrincipal principal,
        string code,
        CancellationToken cancellationToken);

    Task<AccountResult<TwoFactorEnabled>> RegenerateRecoveryCodesAsync(
        ClaimsPrincipal principal,
        string password,
        string code,
        CancellationToken cancellationToken);

    Task<AccountResult<bool>> DisableTwoFactorAsync(
        ClaimsPrincipal principal,
        string password,
        string code,
        CancellationToken cancellationToken);

    Task<AccountResult<bool>> ChangePasswordAsync(
        ClaimsPrincipal principal,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken);

    Task<AccountResult<bool>> RequestEmailChangeAsync(
        ClaimsPrincipal principal,
        string newEmail,
        CancellationToken cancellationToken);

    Task<AccountResult<AccountProfile>> ConfirmEmailChangeAsync(
        ClaimsPrincipal principal,
        string newEmail,
        string token,
        CancellationToken cancellationToken);

    Task<AccountResult<bool>> DeleteAccountAsync(
        ClaimsPrincipal principal,
        string password,
        string? twoFactorCode,
        string? recoveryCode,
        CancellationToken cancellationToken);

    Task ResendEmailVerificationAsync(string email, CancellationToken cancellationToken);
}
