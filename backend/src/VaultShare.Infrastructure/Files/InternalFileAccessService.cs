using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VaultShare.Application.Encryption;
using VaultShare.Application.Files;
using VaultShare.Domain.Files;
using VaultShare.Domain.Notifications;
using VaultShare.Domain.Workspaces;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Files;

internal sealed class InternalFileAccessService(VaultShareDbContext dbContext,
    UserManager<ApplicationUser> userManager) : IInternalFileAccessService
{
    public async Task<FileOperationResult<InternalGrantSummary>> GrantAsync(Guid fileId, string recipientEmail,
        string permission, DateTimeOffset? expiresAt, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var actor = await userManager.GetUserAsync(principal);
        var file = await dbContext.StoredFiles.SingleOrDefaultAsync(item => item.Id == fileId, cancellationToken);
        if (actor is null || file is null) return FileOperationResult<InternalGrantSummary>.Failure(FileOperationStatus.NotFound, "grant.not_found");
        var actorMembership = await MembershipAsync(file.WorkspaceId, actor.Id, cancellationToken);
        if (actorMembership is null || (actorMembership.Role is WorkspaceRole.Member && file.OwnerUserId != actor.Id) || actorMembership.Role == WorkspaceRole.Viewer)
            return FileOperationResult<InternalGrantSummary>.Failure(FileOperationStatus.Forbidden, "grant.forbidden");
        var recipient = await userManager.FindByEmailAsync(recipientEmail.Trim());
        if (recipient is null || await MembershipAsync(file.WorkspaceId, recipient.Id, cancellationToken) is null)
            return FileOperationResult<InternalGrantSummary>.Failure(FileOperationStatus.Invalid, "grant.recipient_not_member");
        if (!Enum.TryParse<InternalFilePermission>(permission, true, out var parsed) || expiresAt <= DateTimeOffset.UtcNow)
            return FileOperationResult<InternalGrantSummary>.Failure(FileOperationStatus.Invalid, "grant.invalid_request");
        var existing = await dbContext.InternalFileGrants.SingleOrDefaultAsync(item => item.StoredFileId == fileId &&
            item.GrantedToUserId == recipient.Id && item.RevokedAt == null, cancellationToken);
        if (existing is not null) return FileOperationResult<InternalGrantSummary>.Failure(FileOperationStatus.Conflict, "grant.already_exists");
        var now = DateTimeOffset.UtcNow;
        var grant = new InternalFileGrant(Guid.CreateVersion7(), file.Id, recipient.Id, parsed, actor.Id, expiresAt, now);
        dbContext.InternalFileGrants.Add(grant);
        dbContext.Notifications.Add(new Notification(Guid.CreateVersion7(), recipient.Id, "InternalFileGranted",
            "Akses file diberikan", $"Anda menerima akses {parsed} ke {file.SafeDisplayFilename}.", true, now));
        await dbContext.SaveChangesAsync(cancellationToken);
        return FileOperationResult<InternalGrantSummary>.Success(ToSummary(grant, recipient.Email ?? recipientEmail.Trim()));
    }

    public async Task<FileOperationResult<bool>> RevokeAsync(Guid grantId, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var actor = await userManager.GetUserAsync(principal);
        var grant = await dbContext.InternalFileGrants.SingleOrDefaultAsync(item => item.Id == grantId, cancellationToken);
        if (actor is null || grant is null) return FileOperationResult<bool>.Failure(FileOperationStatus.NotFound, "grant.not_found");
        var file = await dbContext.StoredFiles.SingleAsync(item => item.Id == grant.StoredFileId, cancellationToken);
        var membership = await MembershipAsync(file.WorkspaceId, actor.Id, cancellationToken);
        if (membership is null || (membership.Role is WorkspaceRole.Member && file.OwnerUserId != actor.Id) || membership.Role == WorkspaceRole.Viewer)
            return FileOperationResult<bool>.Failure(FileOperationStatus.Forbidden, "grant.forbidden");
        grant.Revoke(actor.Id, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return FileOperationResult<bool>.Success(true);
    }

    public async Task<FileOperationResult<InternalFileReservation>> AuthorizeDownloadAsync(Guid fileId,
        ClaimsPrincipal principal, CancellationToken cancellationToken) =>
        await AuthorizeAsync(fileId, principal, requireDownload: true, cancellationToken);

    public async Task<FileOperationResult<InternalFileReservation>> AuthorizePreviewAsync(Guid fileId,
        ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var result = await AuthorizeAsync(fileId, principal, requireDownload: false, cancellationToken);
        if (result.Status != FileOperationStatus.Success || result.Value is null) return result;
        var file = result.Value;
        var previewable = file.DetectedMimeType is "image/png" or "image/jpeg" or "image/webp" or "application/pdf" ||
            file.DetectedMimeType == "text/plain" && file.Size <= 1024 * 1024;
        return previewable ? result : FileOperationResult<InternalFileReservation>.Failure(
            FileOperationStatus.Forbidden, "preview.unsupported_type");
    }

    private async Task<FileOperationResult<InternalFileReservation>> AuthorizeAsync(Guid fileId,
        ClaimsPrincipal principal, bool requireDownload, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        var file = await dbContext.StoredFiles.AsNoTracking().SingleOrDefaultAsync(item => item.Id == fileId &&
            item.AvailabilityStatus == AvailabilityStatus.Available, cancellationToken);
        if (user is null || file is null || file.EncryptionMetadataId is null)
            return FileOperationResult<InternalFileReservation>.Failure(FileOperationStatus.NotFound, "file.not_found");
        var membership = await MembershipAsync(file.WorkspaceId, user.Id, cancellationToken);
        if (membership is null) return FileOperationResult<InternalFileReservation>.Failure(FileOperationStatus.Forbidden, "file.forbidden");
        var privileged = membership.Role is WorkspaceRole.Owner or WorkspaceRole.Admin || file.OwnerUserId == user.Id;
        var granted = privileged || await dbContext.InternalFileGrants.AsNoTracking().AnyAsync(item => item.StoredFileId == file.Id &&
            item.GrantedToUserId == user.Id && (!requireDownload || item.Permission == InternalFilePermission.Download) && item.RevokedAt == null &&
            (item.ExpiresAt == null || item.ExpiresAt > DateTimeOffset.UtcNow), cancellationToken);
        if (!granted) return FileOperationResult<InternalFileReservation>.Failure(FileOperationStatus.Forbidden, "file.forbidden");
        var metadata = await dbContext.FileEncryptionMetadata.AsNoTracking().SingleAsync(item => item.Id == file.EncryptionMetadataId, cancellationToken);
        return FileOperationResult<InternalFileReservation>.Success(new(file.Id, file.SafeDisplayFilename, file.FileSize,
            file.DetectedMimeType ?? "application/octet-stream", file.StoredObjectKey, new FileEncryptionResult(metadata.Algorithm,
                metadata.AlgorithmVersion, metadata.ChunkSize, metadata.BaseNonce, new WrappedDataKey(metadata.WrappedDataKey,
                    metadata.KeyWrapNonce, metadata.KeyWrapAuthenticationTag, metadata.KeyProvider, metadata.KeyIdentifier,
                    metadata.CreatedAt), metadata.CreatedAt)));
    }

    public async Task<FileOperationResult<IReadOnlyList<InternalGrantSummary>>> ListAsync(Guid fileId,
        ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var actor = await userManager.GetUserAsync(principal);
        var file = await dbContext.StoredFiles.AsNoTracking().SingleOrDefaultAsync(item => item.Id == fileId,
            cancellationToken);
        if (actor is null || file is null)
            return FileOperationResult<IReadOnlyList<InternalGrantSummary>>.Failure(FileOperationStatus.NotFound, "grant.not_found");
        var membership = await MembershipAsync(file.WorkspaceId, actor.Id, cancellationToken);
        if (membership is null || membership.Role == WorkspaceRole.Viewer ||
            (membership.Role == WorkspaceRole.Member && file.OwnerUserId != actor.Id))
            return FileOperationResult<IReadOnlyList<InternalGrantSummary>>.Failure(FileOperationStatus.Forbidden, "grant.forbidden");
        var rows = await (from grant in dbContext.InternalFileGrants.AsNoTracking()
                          join user in dbContext.Users.AsNoTracking() on grant.GrantedToUserId equals user.Id
                          where grant.StoredFileId == fileId
                          orderby grant.CreatedAt descending
                          select new InternalGrantSummary(grant.Id, grant.StoredFileId, grant.GrantedToUserId,
                              user.Email ?? string.Empty, grant.Permission.ToString(), grant.ExpiresAt, grant.RevokedAt))
            .ToListAsync(cancellationToken);
        return FileOperationResult<IReadOnlyList<InternalGrantSummary>>.Success(rows);
    }

    private Task<WorkspaceMember?> MembershipAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.WorkspaceMembers.AsNoTracking().SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId &&
            item.UserId == userId && item.RemovedAt == null, cancellationToken);
    private static InternalGrantSummary ToSummary(InternalFileGrant grant, string recipientEmail) => new(grant.Id,
        grant.StoredFileId, grant.GrantedToUserId, recipientEmail, grant.Permission.ToString(), grant.ExpiresAt, grant.RevokedAt);
}
