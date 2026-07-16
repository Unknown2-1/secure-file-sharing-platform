using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VaultShare.Application.Maintenance;
using VaultShare.Application.Storage;
using VaultShare.Domain.Auditing;
using VaultShare.Domain.Files;
using VaultShare.Domain.Notifications;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Maintenance;

internal sealed class MaintenanceService(
    VaultShareDbContext dbContext,
    IObjectStorage objectStorage,
    IConfiguration configuration,
    ILogger<MaintenanceService> logger) : IMaintenanceService
{
    public async Task<MaintenanceResult> RunAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var now = DateTimeOffset.UtcNow;
        var expiredShares = await NotifyExpiredSharesAsync(now, batchSize, cancellationToken);
        var purgedFiles = await PurgeDeletedFilesAsync(now, batchSize, cancellationToken);
        var staleUploads = await FailStaleUploadsAsync(now, batchSize, cancellationToken);
        var sessions = await RemoveExpiredSessionsAsync(now, batchSize, cancellationToken);
        var attempts = await RemoveOldAccessAttemptsAsync(now, batchSize, cancellationToken);
        var notifications = await RemoveOldNotificationsAsync(now, batchSize, cancellationToken);
        var audits = await RemoveOldAuditEventsAsync(now, batchSize, cancellationToken);
        return new(expiredShares, purgedFiles, sessions, attempts, notifications, audits, staleUploads);
    }

    private async Task<int> NotifyExpiredSharesAsync(DateTimeOffset now, int batchSize,
        CancellationToken cancellationToken)
    {
        var shares = await dbContext.Shares.Where(share => share.ExpiresAt <= now &&
                share.ExpirationNotifiedAt == null && !share.IsRevoked)
            .OrderBy(share => share.ExpiresAt).Take(batchSize).ToListAsync(cancellationToken);
        foreach (var share in shares)
        {
            share.MarkExpirationNotified(now);
            dbContext.Notifications.Add(new Notification(Guid.CreateVersion7(), share.CreatedByUserId,
                "ShareExpired", "Share telah kedaluwarsa", $"Share {share.Name} tidak lagi menerima akses baru.",
                true, now));
            dbContext.AuditEvents.Add(SystemAudit(share.WorkspaceId, "ShareExpired", "Share", share.Id, now));
        }

        if (shares.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
        return shares.Count;
    }

    private async Task<int> PurgeDeletedFilesAsync(DateTimeOffset now, int batchSize,
        CancellationToken cancellationToken)
    {
        var fallbackGrace = ReadDuration("DELETED_FILE_GRACE_PERIOD", TimeSpan.FromDays(30));
        var gracePolicies = await dbContext.WorkspaceSettings.AsNoTracking()
            .Select(setting => new { setting.WorkspaceId, setting.DeletedFileGraceDays })
            .ToListAsync(cancellationToken);
        var fallbackDeadline = now - fallbackGrace;
        var files = await dbContext.StoredFiles.Where(file => file.DeletedAt != null &&
                file.DeletedAt <= fallbackDeadline && file.PurgedAt == null &&
                !dbContext.WorkspaceSettings.Any(setting => setting.WorkspaceId == file.WorkspaceId))
            .OrderBy(file => file.DeletedAt).Take(batchSize).ToListAsync(cancellationToken);
        foreach (var policy in gracePolicies)
        {
            if (files.Count >= batchSize) break;
            var deadline = now.AddDays(-policy.DeletedFileGraceDays);
            var rows = await dbContext.StoredFiles.Where(file => file.WorkspaceId == policy.WorkspaceId &&
                    file.DeletedAt != null && file.DeletedAt <= deadline && file.PurgedAt == null)
                .OrderBy(file => file.DeletedAt).Take(batchSize - files.Count).ToListAsync(cancellationToken);
            files.AddRange(rows);
        }
        var purged = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var shareIds = await dbContext.ShareItems.Where(item => item.StoredFileId == file.Id)
                    .Select(item => item.ShareId).ToListAsync(cancellationToken);
                var shares = await dbContext.Shares.Where(share => shareIds.Contains(share.Id) && !share.IsRevoked)
                    .ToListAsync(cancellationToken);
                foreach (var share in shares) share.Revoke(file.OwnerUserId, now);

                await objectStorage.DeleteAsync(file.StoredObjectKey, cancellationToken);
                if (file.EncryptionMetadataId is Guid metadataId)
                {
                    var metadata = await dbContext.FileEncryptionMetadata
                        .SingleOrDefaultAsync(item => item.Id == metadataId, cancellationToken);
                    if (metadata is not null) dbContext.FileEncryptionMetadata.Remove(metadata);
                }

                file.MarkPurged(now);
                dbContext.AuditEvents.Add(SystemAudit(file.WorkspaceId, "FilePurged", "StoredFile", file.Id, now));
                await dbContext.SaveChangesAsync(cancellationToken);
                purged++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Purge failed for file {FileId}", file.Id);
                dbContext.ChangeTracker.Clear();
                break;
            }
        }

        return purged;
    }

    private async Task<int> FailStaleUploadsAsync(DateTimeOffset now, int batchSize,
        CancellationToken cancellationToken)
    {
        var cutoff = now - ReadDuration("PROCESSING_STALE_AFTER", TimeSpan.FromHours(1));
        var uploads = await dbContext.FileUploads.Include(upload => upload.StoredFile)
            .Where(upload => upload.Status == UploadStatus.Processing && upload.UpdatedAt <= cutoff)
            .OrderBy(upload => upload.UpdatedAt).Take(batchSize).ToListAsync(cancellationToken);
        foreach (var upload in uploads)
        {
            upload.Fail(now);
            upload.StoredFile.MarkProcessingFailed();
            TryDeleteTemporaryFile(upload.TemporaryPath, upload.Id);
        }

        if (uploads.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
        return uploads.Count;
    }

    private async Task<int> RemoveExpiredSessionsAsync(DateTimeOffset now, int batchSize,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.DownloadSessions.Where(session => session.ExpiresAt <= now &&
                !dbContext.FileDownloads.Any(download => download.SessionId == session.Id))
            .OrderBy(session => session.ExpiresAt).Take(batchSize).ToListAsync(cancellationToken);
        dbContext.DownloadSessions.RemoveRange(sessions);
        if (sessions.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
        return sessions.Count;
    }

    private Task<int> RemoveOldAccessAttemptsAsync(DateTimeOffset now, int batchSize,
        CancellationToken cancellationToken) => RemoveAsync(
        dbContext.ShareAccessAttempts.Where(item => item.AttemptedAt <= now.AddDays(-ReadDays("ACCESS_ATTEMPT_RETENTION_DAYS", 90)))
            .OrderBy(item => item.AttemptedAt).Take(batchSize), dbContext.ShareAccessAttempts, cancellationToken);

    private Task<int> RemoveOldNotificationsAsync(DateTimeOffset now, int batchSize,
        CancellationToken cancellationToken) => RemoveAsync(
        dbContext.Notifications.Where(item => item.CreatedAt <= now.AddDays(-ReadDays("NOTIFICATION_RETENTION_DAYS", 90)))
            .OrderBy(item => item.CreatedAt).Take(batchSize), dbContext.Notifications, cancellationToken);

    private async Task<int> RemoveOldAuditEventsAsync(DateTimeOffset now, int batchSize,
        CancellationToken cancellationToken)
    {
        var fallbackDays = ReadDays("AUDIT_RETENTION_DAYS", 365);
        var retentionPolicies = await dbContext.WorkspaceSettings.AsNoTracking()
            .Select(setting => new { setting.WorkspaceId, setting.AuditRetentionDays })
            .ToListAsync(cancellationToken);
        var expired = await dbContext.AuditEvents.Where(item => item.Timestamp <= now.AddDays(-fallbackDays) &&
                (item.WorkspaceId == null || !dbContext.WorkspaceSettings.Any(setting =>
                    setting.WorkspaceId == item.WorkspaceId)))
            .OrderBy(item => item.Timestamp).Take(batchSize).ToListAsync(cancellationToken);
        foreach (var policy in retentionPolicies)
        {
            if (expired.Count >= batchSize) break;
            var rows = await dbContext.AuditEvents.Where(item => item.WorkspaceId == policy.WorkspaceId &&
                    item.Timestamp <= now.AddDays(-policy.AuditRetentionDays))
                .OrderBy(item => item.Timestamp).Take(batchSize - expired.Count).ToListAsync(cancellationToken);
            expired.AddRange(rows);
        }
        dbContext.AuditEvents.RemoveRange(expired);
        if (expired.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    private async Task<int> RemoveAsync<T>(IQueryable<T> query, DbSet<T> set,
        CancellationToken cancellationToken) where T : class
    {
        var rows = await query.ToListAsync(cancellationToken);
        set.RemoveRange(rows);
        if (rows.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    private int ReadDays(string key, int fallback) => int.TryParse(configuration[key], out var value) && value > 0
        ? value : fallback;

    private TimeSpan ReadDuration(string key, TimeSpan fallback) =>
        TimeSpan.TryParse(configuration[key], out var value) && value > TimeSpan.Zero ? value : fallback;

    private void TryDeleteTemporaryFile(string path, Guid uploadId)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException exception)
        {
            logger.LogWarning(exception, "Temporary file cleanup deferred for upload {UploadId}", uploadId);
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(exception, "Temporary file cleanup denied for upload {UploadId}", uploadId);
        }
    }

    private static AuditEvent SystemAudit(Guid workspaceId, string action, string targetType,
        Guid targetId, DateTimeOffset now) => new(Guid.CreateVersion7(), workspaceId, null, action,
        targetType, targetId.ToString("D"), now, "system", "VaultShare.Worker", "maintenance", "Success", "{}");
}

internal sealed class StorageConsistencyService(VaultShareDbContext dbContext,
    IObjectStorage objectStorage) : IStorageConsistencyService
{
    public async Task<StorageConsistencyResult> CheckMissingObjectsAsync(int batchSize,
        CancellationToken cancellationToken) => await ReconcileAsync(batchSize, false, cancellationToken);

    public async Task<StorageConsistencyResult> ReconcileAsync(int batchSize, bool deleteOrphans,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var files = await dbContext.StoredFiles.AsNoTracking().Where(file => file.PurgedAt == null &&
                file.EncryptionMetadataId != null && file.AvailabilityStatus == AvailabilityStatus.Available)
            .OrderBy(file => file.Id).Take(batchSize).Select(file => new { file.Id, file.StoredObjectKey })
            .ToListAsync(cancellationToken);
        var missing = new List<Guid>();
        foreach (var file in files)
        {
            if (!await objectStorage.ExistsAsync(file.StoredObjectKey, cancellationToken)) missing.Add(file.Id);
        }

        var objectKeys = await objectStorage.ListKeysAsync("encrypted/", batchSize, cancellationToken);
        var databaseKeys = await dbContext.StoredFiles.AsNoTracking().Where(file => file.PurgedAt == null &&
                objectKeys.Contains(file.StoredObjectKey))
            .Select(file => file.StoredObjectKey).ToListAsync(cancellationToken);
        var known = databaseKeys.ToHashSet(StringComparer.Ordinal);
        var orphans = objectKeys.Where(key => !known.Contains(key)).ToList();
        var deleted = 0;
        if (deleteOrphans)
        {
            foreach (var key in orphans)
            {
                await objectStorage.DeleteAsync(key, cancellationToken);
                deleted++;
            }
        }
        return new(files.Count, missing, objectKeys.Count, orphans.Count, deleted, deleteOrphans);
    }
}
