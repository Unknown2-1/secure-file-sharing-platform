using Microsoft.EntityFrameworkCore;
using VaultShare.Application.Encryption;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Encryption;

internal sealed class EncryptionMetadataRotationService(
    VaultShareDbContext dbContext,
    IKeyEncryptionProvider currentProvider,
    IEncryptionKeyRotationService rotationService) : IEncryptionMetadataRotationService
{
    public async Task<KeyRotationBatchResult> RotateBatchAsync(IKeyEncryptionProvider newProvider, int batchSize,
        bool dryRun, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        var metadataRecords = await dbContext.FileEncryptionMetadata
            .Where(metadata => metadata.KeyIdentifier == currentProvider.KeyIdentifier &&
                               metadata.KeyIdentifier != newProvider.KeyIdentifier)
            .OrderBy(metadata => metadata.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        if (dryRun) return new(metadataRecords.Count, 0, true);

        foreach (var metadata in metadataRecords)
        {
            var current = new WrappedDataKey(metadata.WrappedDataKey, metadata.KeyWrapNonce,
                metadata.KeyWrapAuthenticationTag, metadata.KeyProvider, metadata.KeyIdentifier, metadata.CreatedAt);
            var rotated = await rotationService.RewrapAsync(current, currentProvider, newProvider, cancellationToken);
            metadata.Rewrap(rotated.Ciphertext, rotated.Nonce, rotated.AuthenticationTag,
                rotated.KeyProvider, rotated.KeyIdentifier, DateTimeOffset.UtcNow);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(metadataRecords.Count, metadataRecords.Count, false);
    }
}
