namespace VaultShare.Application.Encryption;

public sealed record WrappedDataKey(byte[] Ciphertext, byte[] Nonce, byte[] AuthenticationTag,
    string KeyProvider, string KeyIdentifier, DateTimeOffset CreatedAt);

public sealed record FileEncryptionResult(string Algorithm, int AlgorithmVersion, int ChunkSize,
    byte[] BaseNonce, WrappedDataKey WrappedDataKey, DateTimeOffset CreatedAt);

public interface IKeyEncryptionProvider
{
    string ProviderName { get; }
    string KeyIdentifier { get; }
    ValueTask<WrappedDataKey> WrapAsync(ReadOnlyMemory<byte> plaintextKey, CancellationToken cancellationToken);
    ValueTask<byte[]> UnwrapAsync(WrappedDataKey wrappedKey, CancellationToken cancellationToken);
}

public interface IFileEncryptionService
{
    Task<FileEncryptionResult> EncryptAsync(Guid fileId, Stream plaintext, Stream ciphertext,
        int chunkSize, CancellationToken cancellationToken);
    Task DecryptAsync(Guid fileId, Stream ciphertext, Stream plaintext,
        FileEncryptionResult metadata, CancellationToken cancellationToken);
}

public interface IEncryptionKeyRotationService
{
    Task<WrappedDataKey> RewrapAsync(WrappedDataKey current, IKeyEncryptionProvider currentProvider,
        IKeyEncryptionProvider newProvider, CancellationToken cancellationToken);
}

public sealed record KeyRotationBatchResult(int Candidates, int Rewrapped, bool DryRun);

public interface IEncryptionMetadataRotationService
{
    Task<KeyRotationBatchResult> RotateBatchAsync(IKeyEncryptionProvider newProvider, int batchSize,
        bool dryRun, CancellationToken cancellationToken);
}
