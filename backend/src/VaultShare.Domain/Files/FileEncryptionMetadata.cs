namespace VaultShare.Domain.Files;

public sealed class FileEncryptionMetadata
{
    private FileEncryptionMetadata() { }

    public FileEncryptionMetadata(Guid id, string algorithm, int algorithmVersion, byte[] wrappedDataKey,
        byte[] keyWrapNonce, byte[] keyWrapAuthenticationTag, string keyProvider, string keyIdentifier,
        int chunkSize, byte[] baseNonce, DateTimeOffset createdAt)
    {
        Id = id;
        Algorithm = algorithm;
        AlgorithmVersion = algorithmVersion;
        WrappedDataKey = wrappedDataKey.ToArray();
        KeyWrapNonce = keyWrapNonce.ToArray();
        KeyWrapAuthenticationTag = keyWrapAuthenticationTag.ToArray();
        KeyProvider = keyProvider;
        KeyIdentifier = keyIdentifier;
        ChunkSize = chunkSize;
        BaseNonce = baseNonce.ToArray();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Algorithm { get; private set; } = string.Empty;
    public int AlgorithmVersion { get; private set; }
    public byte[] WrappedDataKey { get; private set; } = [];
    public byte[] KeyWrapNonce { get; private set; } = [];
    public byte[] KeyWrapAuthenticationTag { get; private set; } = [];
    public string KeyProvider { get; private set; } = string.Empty;
    public string KeyIdentifier { get; private set; } = string.Empty;
    public int ChunkSize { get; private set; }
    public byte[] BaseNonce { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RotatedAt { get; private set; }
    public int EncryptionMetadataVersion { get; private set; } = 1;

    public void Rewrap(byte[] wrappedDataKey, byte[] nonce, byte[] tag, string provider,
        string keyIdentifier, DateTimeOffset rotatedAt)
    {
        WrappedDataKey = wrappedDataKey.ToArray();
        KeyWrapNonce = nonce.ToArray();
        KeyWrapAuthenticationTag = tag.ToArray();
        KeyProvider = provider;
        KeyIdentifier = keyIdentifier;
        RotatedAt = rotatedAt;
        EncryptionMetadataVersion++;
    }
}
