using System.Collections.Concurrent;
using VaultShare.Application.Storage;

namespace VaultShare.Infrastructure.Storage;

internal sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    public async Task PutAsync(string objectKey, Stream content, long contentLength,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length != contentLength) throw new EndOfStreamException();
        if (!_objects.TryAdd(objectKey, buffer.ToArray())) throw new IOException("Object already exists.");
    }

    public Task<Stream> GetStreamAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_objects.TryGetValue(objectKey, out var value)) throw new FileNotFoundException();
        return Task.FromResult<Stream>(new MemoryStream(value, writable: false));
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _objects.TryRemove(objectKey, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult(_objects.ContainsKey(objectKey));

    public Task<ObjectStorageMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult(_objects.TryGetValue(objectKey, out var value)
            ? new ObjectStorageMetadata(value.LongLength, "application/octet-stream", null)
            : null);

    public Task<IReadOnlyList<string>> ListKeysAsync(string prefix, int maximum,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (maximum is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(maximum));
        IReadOnlyList<string> keys = _objects.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).Take(maximum).ToList();
        return Task.FromResult(keys);
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken) => Task.FromResult(true);
}
