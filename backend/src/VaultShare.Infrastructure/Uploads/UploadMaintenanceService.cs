using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VaultShare.Application.Uploads;
using VaultShare.Domain.Files;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Uploads;

internal sealed class UploadMaintenanceService(
    VaultShareDbContext dbContext,
    ILogger<UploadMaintenanceService> logger) : IUploadMaintenanceService
{
    public async Task<int> CleanupAbandonedAsync(int batchSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        var now = DateTimeOffset.UtcNow;
        var uploads = await dbContext.FileUploads
            .Include(upload => upload.StoredFile)
            .Where(upload => upload.Status == UploadStatus.Abandoned ||
                             (upload.ExpiresAt <= now &&
                              (upload.Status == UploadStatus.Pending || upload.Status == UploadStatus.Uploading)))
            .OrderBy(upload => upload.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var upload in uploads)
        {
            upload.Abandon(now);
            upload.StoredFile.MarkUploadAbandoned();
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var upload in uploads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Delete(upload.TemporaryPath);
            }
            catch (IOException exception)
            {
                logger.LogWarning(exception, "Temporary upload cleanup failed for {UploadId}", upload.Id);
            }
            catch (UnauthorizedAccessException exception)
            {
                logger.LogWarning(exception, "Temporary upload cleanup was denied for {UploadId}", upload.Id);
            }
        }

        return uploads.Count;
    }
}
