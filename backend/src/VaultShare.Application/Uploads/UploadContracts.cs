using System.Security.Claims;

namespace VaultShare.Application.Uploads;

public sealed record CreateUploadCommand(Guid WorkspaceId, string Filename, long FileSize,
    string ClientMimeType, string IdempotencyKey);

public sealed record UploadSessionInfo(Guid Id, Guid StoredFileId, Guid WorkspaceId,
    string SafeDisplayFilename, long FileSize, long UploadOffset, string Status, DateTimeOffset ExpiresAt);

public enum UploadOperationStatus { Success, Invalid, Forbidden, NotFound, Conflict, TooLarge, QuotaExceeded }

public sealed record UploadOperationResult<T>(UploadOperationStatus Status, T? Value, string ErrorCode)
{
    public static UploadOperationResult<T> Success(T value) => new(UploadOperationStatus.Success, value, string.Empty);
    public static UploadOperationResult<T> Failure(UploadOperationStatus status, string code) => new(status, default, code);
}

public interface IUploadService
{
    Task<UploadOperationResult<UploadSessionInfo>> GetAsync(Guid uploadId,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<UploadOperationResult<UploadSessionInfo>> CreateAsync(CreateUploadCommand command,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<UploadOperationResult<UploadSessionInfo>> AppendChunkAsync(Guid uploadId, long offset,
        long contentLength, Stream content, ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<UploadOperationResult<UploadSessionInfo>> FinalizeAsync(Guid uploadId, string idempotencyKey,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<UploadOperationResult<bool>> CancelAsync(Guid uploadId, string idempotencyKey,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
}

public interface IUploadMaintenanceService
{
    Task<int> CleanupAbandonedAsync(int batchSize, CancellationToken cancellationToken);
}
