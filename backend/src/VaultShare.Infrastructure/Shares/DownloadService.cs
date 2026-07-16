using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using VaultShare.Application.Encryption;
using VaultShare.Application.Shares;
using VaultShare.Domain.Files;
using VaultShare.Domain.Notifications;
using VaultShare.Domain.Shares;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Shares;

internal sealed class DownloadService(
    VaultShareDbContext dbContext,
    ISecureTokenGenerator tokenGenerator) : IDownloadService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> LocalLocks = new();

    public async Task<ShareOperationResult<DownloadReservation>> ReserveAsync(Guid fileId, string sessionToken,
        CancellationToken cancellationToken)
    {
        if (sessionToken.Length is < 16 or > 128) return Denied();
        var sessionHash = tokenGenerator.Hash(sessionToken);
        var session = await dbContext.DownloadSessions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TokenHash == sessionHash, cancellationToken);
        if (session is null || !tokenGenerator.Verify(sessionToken, session.TokenHash) ||
            session.RevokedAt is not null || session.ExpiresAt <= DateTimeOffset.UtcNow)
            return Denied();

        var gate = LocalLocks.GetOrAdd(session.ShareId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (dbContext.Database.IsRelational())
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var result = await ReserveCoreAsync(session.Id, session.ShareId, fileId, cancellationToken);
                if (result.Status == ShareOperationStatus.Success) await transaction.CommitAsync(cancellationToken);
                else await transaction.RollbackAsync(cancellationToken);
                return result;
            }
            return await ReserveCoreAsync(session.Id, session.ShareId, fileId, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Denied();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task CompleteAsync(Guid downloadId, bool succeeded, CancellationToken cancellationToken)
    {
        var download = await dbContext.FileDownloads.SingleOrDefaultAsync(item => item.Id == downloadId, cancellationToken);
        if (download is null || download.CompletedAt is not null || download.FailedAt is not null) return;
        if (succeeded) download.Complete(DateTimeOffset.UtcNow); else download.Fail(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ShareOperationResult<PreviewReservation>> AuthorizePreviewAsync(Guid fileId,
        string sessionToken, CancellationToken cancellationToken)
    {
        if (sessionToken.Length is < 16 or > 128)
            return ShareOperationResult<PreviewReservation>.Failure(ShareOperationStatus.AccessDenied, "preview.access_denied");
        var sessionHash = tokenGenerator.Hash(sessionToken);
        var session = await dbContext.DownloadSessions.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TokenHash == sessionHash && item.RevokedAt == null && item.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);
        if (session is null || !tokenGenerator.Verify(sessionToken, session.TokenHash)) return PreviewDenied();
        var share = await dbContext.Shares.AsNoTracking().SingleOrDefaultAsync(item => item.Id == session.ShareId, cancellationToken);
        if (share is null || !share.AllowPreview || !share.CanAccess(DateTimeOffset.UtcNow)) return PreviewDenied();
        var file = await (from item in dbContext.ShareItems.AsNoTracking()
                          join storedFile in dbContext.StoredFiles.AsNoTracking() on item.StoredFileId equals storedFile.Id
                          where item.ShareId == share.Id && item.StoredFileId == fileId &&
                                storedFile.AvailabilityStatus == AvailabilityStatus.Available
                          select storedFile).SingleOrDefaultAsync(cancellationToken);
        if (file is null || file.EncryptionMetadataId is null || !PreviewAllowed(file)) return PreviewDenied();
        var metadata = await dbContext.FileEncryptionMetadata.AsNoTracking().SingleAsync(item => item.Id == file.EncryptionMetadataId, cancellationToken);
        return ShareOperationResult<PreviewReservation>.Success(new(file.Id, file.SafeDisplayFilename,
            file.FileSize, file.DetectedMimeType!, file.StoredObjectKey, new FileEncryptionResult(metadata.Algorithm,
                metadata.AlgorithmVersion, metadata.ChunkSize, metadata.BaseNonce, new WrappedDataKey(
                    metadata.WrappedDataKey, metadata.KeyWrapNonce, metadata.KeyWrapAuthenticationTag,
                    metadata.KeyProvider, metadata.KeyIdentifier, metadata.CreatedAt), metadata.CreatedAt)));
    }

    private async Task<ShareOperationResult<DownloadReservation>> ReserveCoreAsync(Guid sessionId, Guid shareId,
        Guid fileId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var share = await dbContext.Shares.SingleOrDefaultAsync(item => item.Id == shareId, cancellationToken);
        if (share is null || !share.CanAccess(now)) return Denied();
        var file = await (from item in dbContext.ShareItems
                          join storedFile in dbContext.StoredFiles on item.StoredFileId equals storedFile.Id
                          where item.ShareId == shareId && item.StoredFileId == fileId &&
                                storedFile.AvailabilityStatus == AvailabilityStatus.Available
                          select storedFile).SingleOrDefaultAsync(cancellationToken);
        if (file is null || file.EncryptionMetadataId is null) return Denied();
        var metadata = await dbContext.FileEncryptionMetadata.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == file.EncryptionMetadataId, cancellationToken);
        if (metadata is null) return Denied();
        if (!share.TryReserveDownload(now)) return Denied();
        if (share.MaximumDownloads is int maximum && share.DownloadCount == maximum)
            dbContext.Notifications.Add(new Notification(Guid.CreateVersion7(), share.CreatedByUserId,
                "ShareDownloadLimitReached", "Batas download tercapai",
                $"{share.Name} telah mencapai batas {maximum} download.", true, now));

        var download = new FileDownload(Guid.CreateVersion7(), share.Id, file.Id, sessionId, now);
        dbContext.FileDownloads.Add(download);
        await dbContext.SaveChangesAsync(cancellationToken);
        var encryptionMetadata = new FileEncryptionResult(metadata.Algorithm, metadata.AlgorithmVersion,
            metadata.ChunkSize, metadata.BaseNonce, new WrappedDataKey(metadata.WrappedDataKey,
                metadata.KeyWrapNonce, metadata.KeyWrapAuthenticationTag, metadata.KeyProvider,
                metadata.KeyIdentifier, metadata.CreatedAt), metadata.CreatedAt);
        return ShareOperationResult<DownloadReservation>.Success(new(download.Id, file.Id,
            file.SafeDisplayFilename, file.FileSize, file.DetectedMimeType ?? "application/octet-stream",
            file.StoredObjectKey, encryptionMetadata));
    }

    private static ShareOperationResult<DownloadReservation> Denied() =>
        ShareOperationResult<DownloadReservation>.Failure(ShareOperationStatus.AccessDenied, "download.access_denied");
    private static ShareOperationResult<PreviewReservation> PreviewDenied() =>
        ShareOperationResult<PreviewReservation>.Failure(ShareOperationStatus.AccessDenied, "preview.access_denied");
    private static bool PreviewAllowed(StoredFile file) => file.DetectedMimeType switch
    {
        "image/png" or "image/jpeg" or "image/webp" or "application/pdf" => true,
        "text/plain" when file.FileSize <= 1024 * 1024 => true,
        _ => false,
    };
}
