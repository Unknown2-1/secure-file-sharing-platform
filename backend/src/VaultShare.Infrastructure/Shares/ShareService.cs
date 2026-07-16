using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using VaultShare.Application.Shares;
using VaultShare.Domain.Files;
using VaultShare.Domain.Notifications;
using VaultShare.Domain.Shares;
using VaultShare.Domain.Workspaces;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Shares;

internal sealed class ShareService(
    VaultShareDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IPasswordHasher<Share> passwordHasher,
    ISecureTokenGenerator tokenGenerator,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment environment) : IShareService
{
    public async Task<ShareOperationResult<ShareCreated>> CreateAsync(CreateShareCommand command,
        ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return ShareOperationResult<ShareCreated>.Failure(ShareOperationStatus.Forbidden, "share.forbidden");
        var now = DateTimeOffset.UtcNow;
        var maxExpiration = TimeSpan.TryParse(configuration["SHARE_MAX_EXPIRATION"], out var configured)
            ? configured : TimeSpan.FromDays(30);
        var fileIds = command.FileIds.Distinct().ToArray();
        if (fileIds.Length is < 1 or > 20 || command.Name.Length is < 1 or > 120 || command.Description?.Length > 1000 ||
            command.ExpiresAt <= now || command.ExpiresAt > now.Add(maxExpiration) || command.StartsAt >= command.ExpiresAt ||
            command.MaximumDownloads is <= 0 or > 1_000_000 || command.Password?.Length is > 128 or > 0 and < 8 ||
            !ValidIdempotencyKey(command.IdempotencyKey))
            return ShareOperationResult<ShareCreated>.Failure(ShareOperationStatus.Invalid, "share.invalid_request");

        var idempotencyHash = HashText(command.IdempotencyKey);
        if (await dbContext.Shares.AnyAsync(share => share.CreatedByUserId == user.Id &&
                                                   share.CreationIdempotencyKeyHash == idempotencyHash, cancellationToken))
            return ShareOperationResult<ShareCreated>.Failure(ShareOperationStatus.Conflict, "share.idempotent_replay");

        var membership = await dbContext.WorkspaceMembers.AsNoTracking().SingleOrDefaultAsync(member =>
            member.WorkspaceId == command.WorkspaceId && member.UserId == user.Id && member.RemovedAt == null, cancellationToken);
        if (membership is null || membership.Role == WorkspaceRole.Viewer)
            return ShareOperationResult<ShareCreated>.Failure(ShareOperationStatus.Forbidden, "share.forbidden");
        if (membership.Role == WorkspaceRole.Member && await dbContext.WorkspaceSettings.AsNoTracking().AnyAsync(setting =>
            setting.WorkspaceId == command.WorkspaceId && !setting.AllowMemberPublicShares, cancellationToken))
            return ShareOperationResult<ShareCreated>.Failure(ShareOperationStatus.Forbidden, "share.member_public_sharing_disabled");
        var files = await dbContext.StoredFiles.Where(file => fileIds.Contains(file.Id) &&
            file.WorkspaceId == command.WorkspaceId && file.AvailabilityStatus == AvailabilityStatus.Available).ToListAsync(cancellationToken);
        if (files.Count != fileIds.Length || (membership.Role == WorkspaceRole.Member && files.Any(file => file.OwnerUserId != user.Id)))
            return ShareOperationResult<ShareCreated>.Failure(ShareOperationStatus.Forbidden, "share.files_unavailable");

        var secretToken = tokenGenerator.Generate(32);
        var share = new Share(Guid.CreateVersion7(), command.WorkspaceId, user.Id, tokenGenerator.Generate(16),
            tokenGenerator.Hash(secretToken), idempotencyHash, command.Name.Trim(), command.Description?.Trim(),
            command.StartsAt, command.ExpiresAt, command.MaximumDownloads, command.IsOneTime, command.AllowPreview, now);
        if (!string.IsNullOrWhiteSpace(command.Password)) share.SetPasswordHash(passwordHasher.HashPassword(share, command.Password));
        dbContext.Shares.Add(share);
        dbContext.ShareItems.AddRange(fileIds.Select(fileId => new ShareItem(share.Id, fileId)));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ShareOperationResult<ShareCreated>.Success(new(share.Id, share.PublicIdentifier, secretToken, share.ExpiresAt));
    }

    public async Task<ShareOperationResult<IReadOnlyList<ShareSummary>>> ListAsync(Guid workspaceId,
        ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return ShareOperationResult<IReadOnlyList<ShareSummary>>.Failure(ShareOperationStatus.Forbidden, "share.forbidden");
        var membership = await dbContext.WorkspaceMembers.AsNoTracking().SingleOrDefaultAsync(member =>
            member.WorkspaceId == workspaceId && member.UserId == user.Id && member.RemovedAt == null, cancellationToken);
        if (membership is null || membership.Role == WorkspaceRole.Viewer)
            return ShareOperationResult<IReadOnlyList<ShareSummary>>.Failure(ShareOperationStatus.Forbidden, "share.forbidden");
        var query = dbContext.Shares.AsNoTracking().Where(share => share.WorkspaceId == workspaceId);
        if (membership.Role == WorkspaceRole.Member) query = query.Where(share => share.CreatedByUserId == user.Id);
        var shares = await query.OrderByDescending(share => share.CreatedAt).Take(200).ToListAsync(cancellationToken);
        var ids = shares.Select(share => share.Id).ToArray();
        var files = await (from item in dbContext.ShareItems.AsNoTracking()
                           join file in dbContext.StoredFiles.AsNoTracking() on item.StoredFileId equals file.Id
                           where ids.Contains(item.ShareId)
                           select new { item.ShareId, File = new SharedFileSummary(file.Id, file.SafeDisplayFilename, file.FileSize, file.DetectedMimeType) })
            .ToListAsync(cancellationToken);
        var result = shares.Select(share => ToSummary(share, files.Where(item => item.ShareId == share.Id).Select(item => item.File).ToList())).ToList();
        return ShareOperationResult<IReadOnlyList<ShareSummary>>.Success(result);
    }

    public async Task<ShareOperationResult<bool>> RevokeAsync(Guid shareId, string idempotencyKey,
        ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!ValidIdempotencyKey(idempotencyKey)) return ShareOperationResult<bool>.Failure(ShareOperationStatus.Invalid, "share.invalid_idempotency_key");
        var user = await userManager.GetUserAsync(principal);
        var share = await dbContext.Shares.SingleOrDefaultAsync(item => item.Id == shareId, cancellationToken);
        if (user is null || share is null) return ShareOperationResult<bool>.Failure(ShareOperationStatus.NotFound, "share.not_found");
        var membership = await dbContext.WorkspaceMembers.AsNoTracking().SingleOrDefaultAsync(member =>
            member.WorkspaceId == share.WorkspaceId && member.UserId == user.Id && member.RemovedAt == null, cancellationToken);
        if (membership is null || (membership.Role is WorkspaceRole.Member && share.CreatedByUserId != user.Id) || membership.Role == WorkspaceRole.Viewer)
            return ShareOperationResult<bool>.Failure(ShareOperationStatus.Forbidden, "share.forbidden");
        share.Revoke(user.Id, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ShareOperationResult<bool>.Success(true);
    }

    public async Task<ShareOperationResult<PublicShareSession>> CreatePublicSessionAsync(string publicIdentifier,
        string secretToken, string? password, CancellationToken cancellationToken)
    {
        if (publicIdentifier.Length > 32 || secretToken.Length > 128 || password?.Length > 128)
            return AccessDenied();
        var share = await dbContext.Shares.AsNoTracking().SingleOrDefaultAsync(item => item.PublicIdentifier == publicIdentifier, cancellationToken);
        if (share is null) return AccessDenied();
        var now = DateTimeOffset.UtcNow;
        var failedAttempts = await dbContext.ShareAccessAttempts.CountAsync(attempt => attempt.ShareId == share.Id &&
            !attempt.Succeeded && attempt.AttemptedAt >= now.AddMinutes(-15), cancellationToken);
        if (failedAttempts >= 5)
        {
            await RecordAttemptAsync(share.Id, false, "share.temporarily_locked", cancellationToken);
            return AccessDenied();
        }
        if (!tokenGenerator.Verify(secretToken, share.SecretTokenHash) || !share.CanAccess(now))
        {
            await RecordAttemptAsync(share.Id, false, "share.access_denied", cancellationToken);
            return AccessDenied();
        }
        if (share.PasswordHash is not null && (string.IsNullOrEmpty(password) ||
            passwordHasher.VerifyHashedPassword(share, share.PasswordHash, password) == PasswordVerificationResult.Failed))
        {
            await RecordAttemptAsync(share.Id, false, "share.access_denied", cancellationToken);
            return AccessDenied();
        }

        var files = await (from item in dbContext.ShareItems.AsNoTracking()
                           join file in dbContext.StoredFiles.AsNoTracking() on item.StoredFileId equals file.Id
                           where item.ShareId == share.Id && file.AvailabilityStatus == AvailabilityStatus.Available
                           select new SharedFileSummary(file.Id, file.SafeDisplayFilename, file.FileSize, file.DetectedMimeType))
            .ToListAsync(cancellationToken);
        if (files.Count == 0) return AccessDenied();
        var token = tokenGenerator.Generate(32);
        var expiresAt = new[] { now.AddMinutes(15), share.ExpiresAt }.Min();
        dbContext.DownloadSessions.Add(new PublicDownloadSession(Guid.CreateVersion7(), share.Id,
            tokenGenerator.Hash(token), now, expiresAt));
        dbContext.ShareAccessAttempts.Add(CreateAttempt(share.Id, true, "share.access_granted"));
        if (!await dbContext.ShareAccessAttempts.AnyAsync(attempt => attempt.ShareId == share.Id && attempt.Succeeded, cancellationToken))
            dbContext.Notifications.Add(new Notification(Guid.CreateVersion7(), share.CreatedByUserId,
                "ShareFirstAccess", "Share pertama kali dibuka", $"{share.Name} baru saja diakses untuk pertama kali.", true, now));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ShareOperationResult<PublicShareSession>.Success(new(token, expiresAt, share.Name,
            share.Description, share.AllowPreview, files));
    }

    private static ShareOperationResult<PublicShareSession> AccessDenied() =>
        ShareOperationResult<PublicShareSession>.Failure(ShareOperationStatus.AccessDenied, "share.access_denied");
    private static bool ValidIdempotencyKey(string value) => value.Length is >= 8 and <= 128 && value.All(character => character is >= '!' and <= '~');
    private static string HashText(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private async Task RecordAttemptAsync(Guid shareId, bool succeeded, string resultCode, CancellationToken cancellationToken)
    {
        dbContext.ShareAccessAttempts.Add(CreateAttempt(shareId, succeeded, resultCode));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    private ShareAccessAttempt CreateAttempt(Guid shareId, bool succeeded, string resultCode)
    {
        var context = httpContextAccessor.HttpContext;
        var address = context?.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        byte[] key = environment.IsEnvironment("Testing")
            ? SHA256.HashData("vaultshare-test-only-ip-key"u8.ToArray())
            : Convert.FromBase64String(configuration["PRIVACY_IP_HASH_KEY"]!);
        using var hmac = new HMACSHA256(key);
        var ipHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(address)));
        CryptographicOperations.ZeroMemory(key);
        var userAgent = context?.Request.Headers.UserAgent.ToString() ?? string.Empty;
        return new(Guid.CreateVersion7(), shareId, succeeded, resultCode, ipHash,
            userAgent[..Math.Min(userAgent.Length, 256)], DateTimeOffset.UtcNow);
    }
    private static ShareSummary ToSummary(Share share, IReadOnlyList<SharedFileSummary> files) => new(share.Id,
        share.WorkspaceId, share.Name, share.Description, share.StartsAt, share.ExpiresAt, share.MaximumDownloads,
        share.DownloadCount, share.IsOneTime, share.IsRevoked, share.PasswordHash is not null, share.AllowPreview,
        share.CreatedAt, share.LastAccessedAt, files);
}
