using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VaultShare.Application.Authentication;
using VaultShare.Application.Notifications;
using VaultShare.Domain.Workspaces;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Identity;

internal sealed class AccountService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    VaultShareDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    IHostEnvironment environment,
    IEmailService emailService,
    ILogger<AccountService> logger) : IAccountService
{
    public async Task<AccountResult<AccountProfile>> RegisterAsync(
        RegisterAccountCommand command,
        CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = command.Email.Trim(),
            Email = command.Email.Trim(),
            DisplayName = command.DisplayName.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var creation = await userManager.CreateAsync(user, command.Password);
        if (!creation.Succeeded)
        {
            return AccountResult<AccountProfile>.Failure("account.registration_failed");
        }

        try
        {
            var workspace = new Workspace(
                Guid.CreateVersion7(),
                $"{user.DisplayName} Personal",
                user.Id,
                DateTimeOffset.UtcNow);
            dbContext.Workspaces.Add(workspace);
            dbContext.WorkspaceMembers.Add(new WorkspaceMember(
                workspace.Id,
                user.Id,
                WorkspaceRole.Owner,
                DateTimeOffset.UtcNow));
            await dbContext.SaveChangesAsync(cancellationToken);

            var verificationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var safeName = HtmlEncoder.Default.Encode(user.DisplayName);
            var safeToken = HtmlEncoder.Default.Encode(verificationToken);
            try
            {
                await emailService.SendAsync(new EmailMessage(
                    user.Email,
                    "Verifikasi akun VaultShare",
                    $"Halo {user.DisplayName},\n\nMasukkan kode verifikasi berikut di VaultShare:\n\n{verificationToken}\n\nJika Anda tidak membuat akun ini, abaikan email ini.",
                    $"<p>Halo {safeName},</p><p>Masukkan kode verifikasi berikut di VaultShare:</p><p><code>{safeToken}</code></p><p>Jika Anda tidak membuat akun ini, abaikan email ini.</p>"),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Email verification delivery failed for user {UserId}", user.Id);
            }
        }
        catch
        {
            await userManager.DeleteAsync(user);
            throw;
        }

        return AccountResult<AccountProfile>.Success(ToProfile(user));
    }

    public async Task<AccountResult<bool>> VerifyEmailAsync(
        string email,
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || user.DeletedAt is not null)
        {
            return AccountResult<bool>.Failure("account.verification_failed");
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded
            ? AccountResult<bool>.Success(true)
            : AccountResult<bool>.Failure("account.verification_failed");
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || user.DeletedAt is not null || !await userManager.IsEmailConfirmedAsync(user))
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var safeName = HtmlEncoder.Default.Encode(user.DisplayName);
        var safeToken = HtmlEncoder.Default.Encode(token);
        try
        {
            await emailService.SendAsync(new EmailMessage(
                user.Email ?? string.Empty,
                "Reset password VaultShare",
                $"Halo {user.DisplayName},\n\nMasukkan kode reset berikut di VaultShare:\n\n{token}\n\nJika Anda tidak meminta reset, abaikan email ini dan periksa sesi aktif Anda.",
                $"<p>Halo {safeName},</p><p>Masukkan kode reset berikut di VaultShare:</p><p><code>{safeToken}</code></p><p>Jika Anda tidak meminta reset, abaikan email ini dan periksa sesi aktif Anda.</p>"),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Password reset delivery failed for user {UserId}", user.Id);
        }
    }

    public async Task<AccountResult<bool>> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || user.DeletedAt is not null)
        {
            return AccountResult<bool>.Failure("account.password_reset_failed");
        }

        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            return AccountResult<bool>.Failure("account.password_reset_failed");
        }

        var activeSessions = await dbContext.UserSessions
            .Where(session => session.UserId == user.Id && session.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in activeSessions)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return AccountResult<bool>.Success(true);
    }

    public async Task<AccountResult<AccountProfile>> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(command.Email.Trim());
        if (user is null || user.DeletedAt is not null)
        {
            return AccountResult<AccountProfile>.Failure("account.invalid_credentials");
        }

        var passwordCheck = await signInManager.PasswordSignInAsync(user, command.Password, isPersistent: false, lockoutOnFailure: true);
        if (passwordCheck.RequiresTwoFactor)
        {
            return AccountResult<AccountProfile>.Failure("account.two_factor_required");
        }

        if (!passwordCheck.Succeeded)
        {
            return AccountResult<AccountProfile>.Failure("account.invalid_credentials");
        }

        await signInManager.SignOutAsync();
        return await CompleteSignInAsync(user, cancellationToken);
    }

    public async Task<AccountResult<AccountProfile>> CompleteTwoFactorLoginAsync(
        string? code,
        string? recoveryCode,
        CancellationToken cancellationToken)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return AccountResult<AccountProfile>.Failure("account.two_factor_failed");
        }

        var succeeded = false;
        if (!string.IsNullOrWhiteSpace(code))
        {
            succeeded = await userManager.VerifyTwoFactorTokenAsync(
                user,
                TokenOptions.DefaultAuthenticatorProvider,
                code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal));
        }
        else if (!string.IsNullOrWhiteSpace(recoveryCode))
        {
            var result = await userManager.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode);
            succeeded = result.Succeeded;
        }

        if (!succeeded)
        {
            return AccountResult<AccountProfile>.Failure("account.two_factor_failed");
        }

        if (httpContextAccessor.HttpContext is not null)
        {
            await httpContextAccessor.HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
        }

        return await CompleteSignInAsync(user, cancellationToken);
    }

    public async Task<AccountProfile?> GetCurrentAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        var sessionId = GetSessionId(principal);
        if (user is null || sessionId is null || user.DeletedAt is not null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var active = await dbContext.UserSessions.AnyAsync(
            session => session.Id == sessionId &&
                       session.UserId == user.Id &&
                       session.RevokedAt == null &&
                       session.ExpiresAt > now,
            cancellationToken);

        return active ? ToProfile(user) : null;
    }

    public async Task LogoutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var sessionId = GetSessionId(principal);
        if (sessionId is not null)
        {
            var session = await dbContext.UserSessions.SingleOrDefaultAsync(
                candidate => candidate.Id == sessionId,
                cancellationToken);
            if (session is not null && session.RevokedAt is null)
            {
                session.RevokedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await signInManager.SignOutAsync();
    }

    public async Task<IReadOnlyList<SessionInfo>> ListSessionsAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
        var currentSessionId = GetSessionId(principal);
        return await dbContext.UserSessions
            .Where(session => session.UserId == user.Id)
            .OrderByDescending(session => session.LastSeenAt)
            .Select(session => new SessionInfo(
                session.Id,
                session.CreatedAt,
                session.LastSeenAt,
                session.ExpiresAt,
                session.RevokedAt,
                session.UserAgent,
                session.Id == currentSessionId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeSessionAsync(
        Guid sessionId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
        var session = await dbContext.UserSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == sessionId && candidate.UserId == user.Id,
            cancellationToken);
        if (session is null)
        {
            return false;
        }

        if (session.RevokedAt is null)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<TwoFactorSetup> GetTwoFactorSetupAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        var sharedKey = key ?? throw new InvalidOperationException("Authenticator key generation failed.");
        var issuer = Uri.EscapeDataString("VaultShare");
        var account = Uri.EscapeDataString(user.Email ?? user.Id.ToString("D"));
        var uri = $"otpauth://totp/{issuer}:{account}?secret={sharedKey}&issuer={issuer}&digits=6";
        return new TwoFactorSetup(sharedKey, uri);
    }

    public async Task<AccountResult<TwoFactorEnabled>> EnableTwoFactorAsync(
        ClaimsPrincipal principal,
        string code,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
        var normalizedCode = code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        var valid = await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, normalizedCode);
        if (!valid)
        {
            return AccountResult<TwoFactorEnabled>.Failure("account.two_factor_invalid_code");
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return AccountResult<TwoFactorEnabled>.Success(new TwoFactorEnabled(codes?.ToArray() ?? []));
    }

    public async Task<AccountResult<TwoFactorEnabled>> RegenerateRecoveryCodesAsync(
        ClaimsPrincipal principal,
        string password,
        string code,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
        if (!await userManager.GetTwoFactorEnabledAsync(user) ||
            !await userManager.CheckPasswordAsync(user, password) ||
            !await VerifyAuthenticatorCodeAsync(user, code))
        {
            return AccountResult<TwoFactorEnabled>.Failure("account.two_factor_verification_failed");
        }

        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return AccountResult<TwoFactorEnabled>.Success(new TwoFactorEnabled(codes?.ToArray() ?? []));
    }

    public async Task<AccountResult<bool>> DisableTwoFactorAsync(
        ClaimsPrincipal principal,
        string password,
        string code,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
        if (!await userManager.GetTwoFactorEnabledAsync(user) ||
            !await userManager.CheckPasswordAsync(user, password) ||
            !await VerifyAuthenticatorCodeAsync(user, code))
        {
            return AccountResult<bool>.Failure("account.two_factor_verification_failed");
        }

        var disabled = await userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disabled.Succeeded)
        {
            return AccountResult<bool>.Failure("account.two_factor_disable_failed");
        }

        await userManager.ResetAuthenticatorKeyAsync(user);
        await RotateSessionsAsync(user, cancellationToken);
        return AccountResult<bool>.Success(true);
    }

    public async Task<AccountResult<bool>> ChangePasswordAsync(
        ClaimsPrincipal principal,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            return AccountResult<bool>.Failure("account.password_change_failed");
        }

        await RotateSessionsAsync(user, cancellationToken);
        return AccountResult<bool>.Success(true);
    }

    public async Task<AccountResult<bool>> RequestEmailChangeAsync(
        ClaimsPrincipal principal,
        string newEmail,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
        var existing = await userManager.FindByEmailAsync(newEmail.Trim());
        if (existing is not null && existing.Id != user.Id)
        {
            return AccountResult<bool>.Failure("account.email_change_failed");
        }

        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail.Trim());
        var safeName = HtmlEncoder.Default.Encode(user.DisplayName);
        var safeToken = HtmlEncoder.Default.Encode(token);
        try
        {
            await emailService.SendAsync(new EmailMessage(
                newEmail.Trim(),
                "Konfirmasi email baru VaultShare",
                $"Halo {user.DisplayName},\n\nMasukkan kode perubahan email berikut di VaultShare:\n\n{token}\n\nJika Anda tidak meminta perubahan ini, periksa sesi aktif Anda.",
                $"<p>Halo {safeName},</p><p>Masukkan kode perubahan email berikut di VaultShare:</p><p><code>{safeToken}</code></p><p>Jika Anda tidak meminta perubahan ini, periksa sesi aktif Anda.</p>"),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Email change delivery failed for user {UserId}", user.Id);
        }

        return AccountResult<bool>.Success(true);
    }

    public async Task<AccountResult<AccountProfile>> ConfirmEmailChangeAsync(
        ClaimsPrincipal principal,
        string newEmail,
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
        var result = await userManager.ChangeEmailAsync(user, newEmail.Trim(), token);
        if (!result.Succeeded)
        {
            return AccountResult<AccountProfile>.Failure("account.email_change_failed");
        }

        await RotateSessionsAsync(user, cancellationToken);
        return AccountResult<AccountProfile>.Success(ToProfile(user));
    }

    public async Task<AccountResult<bool>> DeleteAccountAsync(
        ClaimsPrincipal principal,
        string password,
        string? twoFactorCode,
        string? recoveryCode,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
        if (!await userManager.CheckPasswordAsync(user, password) ||
            !await VerifyDeletionSecondFactorAsync(user, twoFactorCode, recoveryCode))
        {
            return AccountResult<bool>.Failure("account.deletion_failed");
        }

        var ownedWorkspaceIds = await dbContext.WorkspaceMembers
            .Where(member => member.UserId == user.Id &&
                             member.Role == WorkspaceRole.Owner &&
                             member.RemovedAt == null)
            .Select(member => member.WorkspaceId)
            .ToListAsync(cancellationToken);

        var ownsSharedWorkspace = await dbContext.WorkspaceMembers.AnyAsync(
            member => ownedWorkspaceIds.Contains(member.WorkspaceId) &&
                      member.UserId != user.Id &&
                      member.RemovedAt == null,
            cancellationToken);
        if (ownsSharedWorkspace)
        {
            return AccountResult<bool>.Failure("account.owns_shared_workspace");
        }

        var now = DateTimeOffset.UtcNow;
        var ownedWorkspaces = await dbContext.Workspaces
            .Where(workspace => ownedWorkspaceIds.Contains(workspace.Id) && workspace.DeletedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var workspace in ownedWorkspaces) workspace.Delete(now);

        var memberships = await dbContext.WorkspaceMembers
            .Where(member => member.UserId == user.Id && member.RemovedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var membership in memberships) membership.Remove(now);

        var sessions = await dbContext.UserSessions
            .Where(session => session.UserId == user.Id && session.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions) session.RevokedAt = now;

        user.EmailConfirmed = false;
        user.UserName = $"deleted-{user.Id:N}";
        user.DisplayName = "Akun dihapus";
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.PasswordHash = null;
        user.TwoFactorEnabled = false;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.DeletedAt = now;

        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            return AccountResult<bool>.Failure("account.deletion_failed");
        }

        // Identity's unique-email validator rejects null during UpdateAsync. Clear the
        // address only after the validated identity update, then persist the anonymized value.
        user.Email = null;
        user.NormalizedEmail = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await signInManager.SignOutAsync();
        return AccountResult<bool>.Success(true);
    }

    public async Task ResendEmailVerificationAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || user.DeletedAt is not null || await userManager.IsEmailConfirmedAsync(user))
        {
            return;
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var safeName = HtmlEncoder.Default.Encode(user.DisplayName);
        var safeToken = HtmlEncoder.Default.Encode(token);
        try
        {
            await emailService.SendAsync(new EmailMessage(
                user.Email ?? string.Empty,
                "Verifikasi akun VaultShare",
                $"Halo {user.DisplayName},\n\nMasukkan kode verifikasi berikut di VaultShare:\n\n{token}",
                $"<p>Halo {safeName},</p><p>Masukkan kode verifikasi berikut di VaultShare:</p><p><code>{safeToken}</code></p>"),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Email verification redelivery failed for user {UserId}", user.Id);
        }
    }

    private string HashIpAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return string.Empty;

        var configuredKey = configuration["PRIVACY_IP_HASH_KEY"];
        byte[] key;
        if (environment.IsEnvironment("Testing"))
        {
            key = SHA256.HashData("vaultshare-test-only-ip-key"u8.ToArray());
        }
        else if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            key = Convert.FromBase64String(configuredKey);
        }
        else
        {
            throw new InvalidOperationException("PRIVACY_IP_HASH_KEY is required.");
        }

        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(address)));
    }

    private static Guid? GetSessionId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(SessionClaims.SessionId), out var id) ? id : null;

    private static AccountProfile ToProfile(ApplicationUser user) =>
        new(user.Id, user.Email ?? string.Empty, user.DisplayName);

    private async Task<AccountResult<AccountProfile>> CompleteSignInAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new UserSession
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.AddHours(8),
            UserAgent = Truncate(httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString(), 256),
            IpAddressHash = HashIpAddress(httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString()),
        };

        user.LastLoginAt = now;
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        await signInManager.SignInWithClaimsAsync(
            user,
            isPersistent: false,
            [new Claim(SessionClaims.SessionId, session.Id.ToString("D"))]);
        return AccountResult<AccountProfile>.Success(ToProfile(user));
    }

    private async Task RotateSessionsAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var sessions = await dbContext.UserSessions
            .Where(session => session.UserId == user.Id && session.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions) session.RevokedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await signInManager.SignOutAsync();
        _ = await CompleteSignInAsync(user, cancellationToken);
    }

    private async Task<bool> VerifyDeletionSecondFactorAsync(
        ApplicationUser user,
        string? twoFactorCode,
        string? recoveryCode)
    {
        if (!await userManager.GetTwoFactorEnabledAsync(user)) return true;

        if (!string.IsNullOrWhiteSpace(twoFactorCode))
        {
            var normalizedCode = twoFactorCode
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal);
            return await userManager.VerifyTwoFactorTokenAsync(
                user,
                TokenOptions.DefaultAuthenticatorProvider,
                normalizedCode);
        }

        if (!string.IsNullOrWhiteSpace(recoveryCode))
        {
            return (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode)).Succeeded;
        }

        return false;
    }

    private async Task<bool> VerifyAuthenticatorCodeAsync(ApplicationUser user, string code)
    {
        var normalizedCode = code
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return await userManager.VerifyTwoFactorTokenAsync(
            user,
            TokenOptions.DefaultAuthenticatorProvider,
            normalizedCode);
    }

    private static string Truncate(string? value, int maximumLength) =>
        string.IsNullOrEmpty(value) ? string.Empty : value[..Math.Min(value.Length, maximumLength)];
}
