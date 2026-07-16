using System.IO.Pipelines;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using VaultShare.Application.Storage;

namespace VaultShare.Infrastructure.Storage;

internal sealed class MinioObjectStorage(IMinioClient client, string bucket) : IObjectStorage
{
    public async Task PutAsync(string objectKey, Stream content, long contentLength,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        ValidateKey(objectKey);
        if (!content.CanRead || contentLength < 0) throw new ArgumentException("Readable content and its length are required.");
        if (await ExistsAsync(objectKey, cancellationToken)) throw new IOException("Object already exists.");
        var headers = metadata is null ? null : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        var args = new PutObjectArgs().WithBucket(bucket).WithObject(objectKey).WithStreamData(content)
            .WithObjectSize(contentLength).WithContentType("application/octet-stream");
        if (headers is not null) args.WithHeaders(headers);
        await client.PutObjectAsync(args, cancellationToken);
    }

    public Task<Stream> GetStreamAsync(string objectKey, CancellationToken cancellationToken)
    {
        ValidateKey(objectKey);
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 1024 * 1024, resumeWriterThreshold: 512 * 1024));
        _ = PumpObjectAsync(objectKey, pipe, cancellationToken);
        return Task.FromResult<Stream>(pipe.Reader.AsStream());
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        ValidateKey(objectKey);
        return client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(objectKey), cancellationToken);
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken) =>
        await GetMetadataAsync(objectKey, cancellationToken) is not null;

    public async Task<ObjectStorageMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken)
    {
        ValidateKey(objectKey);
        try
        {
            var result = await client.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(objectKey), cancellationToken);
            return new(result.Size, result.ContentType ?? "application/octet-stream", result.ETag);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string prefix, int maximum,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prefix) || !prefix.StartsWith("encrypted/", StringComparison.Ordinal))
            throw new ArgumentException("Storage listing prefix is invalid.", nameof(prefix));
        if (maximum is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(maximum));
        var keys = new List<string>();
        var args = new ListObjectsArgs().WithBucket(bucket).WithPrefix(prefix).WithRecursive(true);
        await foreach (var item in client.ListObjectsEnumAsync(args, cancellationToken))
        {
            keys.Add(item.Key);
            if (keys.Count >= maximum) break;
        }
        return keys;
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken) =>
        client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), cancellationToken);

    private async Task PumpObjectAsync(string objectKey, Pipe pipe, CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            var args = new GetObjectArgs().WithBucket(bucket).WithObject(objectKey)
                .WithCallbackStream(async source =>
                    await source.CopyToAsync(pipe.Writer.AsStream(leaveOpen: true), cancellationToken));
            await client.GetObjectAsync(args, cancellationToken);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            await pipe.Writer.CompleteAsync(failure);
        }
    }

    private static void ValidateKey(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey) || objectKey.Length > 512 || objectKey.Contains("..", StringComparison.Ordinal) ||
            !objectKey.StartsWith("encrypted/", StringComparison.Ordinal))
            throw new ArgumentException("Object key is invalid.", nameof(objectKey));
    }
}
