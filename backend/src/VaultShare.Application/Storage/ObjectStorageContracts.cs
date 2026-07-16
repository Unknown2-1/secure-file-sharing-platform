namespace VaultShare.Application.Storage;

public sealed record ObjectStorageMetadata(long Size, string ContentType, string? EntityTag);

public interface IObjectStorage
{
    Task PutAsync(string objectKey, Stream content, long contentLength,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken);
    Task<Stream> GetStreamAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken);
    Task<ObjectStorageMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListKeysAsync(string prefix, int maximum,
        CancellationToken cancellationToken);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken);
}
