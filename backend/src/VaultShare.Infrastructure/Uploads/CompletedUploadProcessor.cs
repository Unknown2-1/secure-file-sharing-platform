using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VaultShare.Application.Encryption;
using VaultShare.Application.Scanning;
using VaultShare.Application.Storage;
using VaultShare.Application.Uploads;
using VaultShare.Domain.Files;
using VaultShare.Domain.Notifications;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Uploads;

internal sealed class CompletedUploadProcessor(
    VaultShareDbContext dbContext,
    IFileContentInspector inspector,
    IMalwareScanner scanner,
    IFileEncryptionService encryption,
    IObjectStorage objectStorage,
    ILogger<CompletedUploadProcessor> logger) : ICompletedUploadProcessor
{
    private const int EncryptionChunkSize = 1024 * 1024;

    public async Task<int> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        var ids = await dbContext.FileUploads.AsNoTracking()
            .Where(upload => upload.Status == UploadStatus.Uploaded)
            .OrderBy(upload => upload.CreatedAt)
            .Select(upload => upload.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        var processed = 0;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await ProcessOneAsync(id, cancellationToken)) processed++;
        }
        return processed;
    }

    private async Task<bool> ProcessOneAsync(Guid uploadId, CancellationToken cancellationToken)
    {
        var upload = await dbContext.FileUploads.Include(candidate => candidate.StoredFile)
            .SingleOrDefaultAsync(candidate => candidate.Id == uploadId, cancellationToken);
        if (upload?.Status != UploadStatus.Uploaded) return false;
        upload.StartProcessing(DateTimeOffset.UtcNow);
        upload.StoredFile.MarkProcessing();
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }

        var file = upload.StoredFile;
        string? uploadedObjectKey = null;
        var cipherPath = $"{upload.TemporaryPath}.{Guid.NewGuid():N}.cipher";
        try
        {
            await using var plaintext = new FileStream(upload.TemporaryPath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var inspection = await inspector.InspectAsync(file.SafeDisplayFilename, plaintext, cancellationToken);
            if (!inspection.IsAllowed || inspection.DetectedMimeType is null)
            {
                upload.Fail(DateTimeOffset.UtcNow);
                file.MarkProcessingFailed();
                AddNotification(file.OwnerUserId, "FileValidationFailed", "File tidak dapat diproses",
                    $"{file.SafeDisplayFilename} tidak cocok dengan tipe file yang diizinkan.");
                await dbContext.SaveChangesAsync(cancellationToken);
                File.Delete(upload.TemporaryPath);
                return true;
            }
            file.MarkValidated(inspection.DetectedMimeType, inspection.Sha256Hash);
            file.MarkScanning();
            await dbContext.SaveChangesAsync(cancellationToken);

            plaintext.Position = 0;
            var scan = await scanner.ScanAsync(plaintext, cancellationToken);
            var scanStatus = scan.Outcome switch
            {
                MalwareScanOutcome.Clean => MalwareScanStatus.Clean,
                MalwareScanOutcome.Infected => MalwareScanStatus.Infected,
                MalwareScanOutcome.ScannerUnavailable => MalwareScanStatus.ScannerUnavailable,
                _ => MalwareScanStatus.Failed,
            };
            dbContext.MalwareScanResults.Add(new MalwareScanRecord(Guid.CreateVersion7(), file.Id,
                scanStatus, "ClamAV", scan.SafeSignature, DateTimeOffset.UtcNow));
            if (scan.Outcome != MalwareScanOutcome.Clean)
            {
                upload.Fail(DateTimeOffset.UtcNow);
                file.MarkScanFailure(scanStatus, scan.Outcome == MalwareScanOutcome.Infected);
                AddNotification(file.OwnerUserId, scan.Outcome == MalwareScanOutcome.Infected ? "MalwareDetected" : "MalwareScanFailed",
                    scan.Outcome == MalwareScanOutcome.Infected ? "File dikarantina" : "Pemindaian file gagal",
                    $"{file.SafeDisplayFilename} tidak tersedia karena pemeriksaan keamanan tidak menghasilkan status clean.");
                await dbContext.SaveChangesAsync(cancellationToken);
                plaintext.Close();
                File.Delete(upload.TemporaryPath);
                return true;
            }

            file.MarkScanClean();
            file.MarkEncryptionStarted();
            await dbContext.SaveChangesAsync(cancellationToken);
            plaintext.Position = 0;
            await using var cipher = new FileStream(cipherPath, FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var encryptionResult = await encryption.EncryptAsync(file.Id, plaintext, cipher, EncryptionChunkSize, cancellationToken);
            cipher.Position = 0;
            if (await objectStorage.ExistsAsync(file.StoredObjectKey, cancellationToken))
                throw new IOException("Generated object key already exists.");
            await objectStorage.PutAsync(file.StoredObjectKey, cipher, cipher.Length,
                new Dictionary<string, string> { ["algorithm"] = encryptionResult.Algorithm, ["version"] = encryptionResult.AlgorithmVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                cancellationToken);
            uploadedObjectKey = file.StoredObjectKey;

            plaintext.Close();
            File.Delete(upload.TemporaryPath);
            var metadata = new FileEncryptionMetadata(Guid.CreateVersion7(), encryptionResult.Algorithm,
                encryptionResult.AlgorithmVersion, encryptionResult.WrappedDataKey.Ciphertext,
                encryptionResult.WrappedDataKey.Nonce, encryptionResult.WrappedDataKey.AuthenticationTag,
                encryptionResult.WrappedDataKey.KeyProvider, encryptionResult.WrappedDataKey.KeyIdentifier,
                encryptionResult.ChunkSize, encryptionResult.BaseNonce, encryptionResult.CreatedAt);
            dbContext.FileEncryptionMetadata.Add(metadata);
            file.MarkEncrypted(metadata.Id);
            file.MarkAvailable(DateTimeOffset.UtcNow);
            upload.Complete(DateTimeOffset.UtcNow);
            AddNotification(file.OwnerUserId, "FileAvailable", "File siap digunakan",
                $"{file.SafeDisplayFilename} telah dipindai, dienkripsi, dan tersedia.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Completed upload processing failed for {UploadId} and {FileId}", upload.Id, file.Id);
            if (uploadedObjectKey is not null)
            {
                try { await objectStorage.DeleteAsync(uploadedObjectKey, cancellationToken); }
                catch (Exception cleanupException) { logger.LogWarning(cleanupException, "Object rollback failed for {FileId}", file.Id); }
                var uncommittedMetadata = dbContext.ChangeTracker.Entries<FileEncryptionMetadata>()
                    .Where(entry => entry.State == EntityState.Added).ToList();
                foreach (var entry in uncommittedMetadata) entry.State = EntityState.Detached;
                file.MarkEncryptionStorageFailed();
            }
            upload.Fail(DateTimeOffset.UtcNow);
            file.MarkProcessingFailed();
            AddNotification(file.OwnerUserId, "FileProcessingFailed", "Pemrosesan file gagal",
                $"{file.SafeDisplayFilename} belum tersedia. Coba unggah kembali atau hubungi dukungan.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        finally
        {
            try { File.Delete(cipherPath); }
            catch (IOException exception) { logger.LogWarning(exception, "Cipher temporary cleanup failed for {FileId}", file.Id); }
        }
    }

    private void AddNotification(Guid userId, string type, string title, string message) =>
        dbContext.Notifications.Add(new Notification(Guid.CreateVersion7(), userId, type, title, message,
            emailRequested: true, DateTimeOffset.UtcNow));
}
