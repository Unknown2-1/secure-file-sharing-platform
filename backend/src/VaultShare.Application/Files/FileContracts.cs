using System.Security.Claims;
using VaultShare.Application.Encryption;

namespace VaultShare.Application.Files;

public sealed record FileSummary(Guid Id, Guid WorkspaceId, Guid OwnerUserId, string Filename,
    long Size, string? DetectedMimeType, string UploadStatus, string MalwareScanStatus,
    string EncryptionStatus, string AvailabilityStatus, DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt, DateTimeOffset? DeletedAt);

public sealed record FilePage(IReadOnlyList<FileSummary> Items, int Page, int PageSize, long Total);
public enum FileOperationStatus { Success, Forbidden, NotFound, Conflict, Invalid }
public sealed record FileOperationResult<T>(FileOperationStatus Status, T? Value, string ErrorCode)
{
    public static FileOperationResult<T> Success(T value) => new(FileOperationStatus.Success, value, string.Empty);
    public static FileOperationResult<T> Failure(FileOperationStatus status, string code) => new(status, default, code);
}

public interface IFileService
{
    Task<FileOperationResult<FilePage>> ListAsync(Guid workspaceId, string? search, string? status,
        int page, int pageSize, ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<FileOperationResult<FileSummary>> GetAsync(Guid fileId, ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<FileOperationResult<bool>> DeleteAsync(Guid fileId, string idempotencyKey,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<FileOperationResult<bool>> RestoreAsync(Guid fileId, string idempotencyKey,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<FileOperationResult<bool>> PurgeAsync(Guid fileId, string idempotencyKey,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
}

public sealed record InternalGrantSummary(Guid Id, Guid FileId, Guid UserId, string RecipientEmail, string Permission,
    DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt);
public sealed record InternalFileReservation(Guid FileId, string Filename, long Size, string DetectedMimeType,
    string ObjectKey, FileEncryptionResult EncryptionMetadata);
public interface IInternalFileAccessService
{
    Task<FileOperationResult<InternalGrantSummary>> GrantAsync(Guid fileId, string recipientEmail,
        string permission, DateTimeOffset? expiresAt, ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<FileOperationResult<bool>> RevokeAsync(Guid grantId, ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<FileOperationResult<IReadOnlyList<InternalGrantSummary>>> ListAsync(Guid fileId,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<FileOperationResult<InternalFileReservation>> AuthorizeDownloadAsync(Guid fileId,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<FileOperationResult<InternalFileReservation>> AuthorizePreviewAsync(Guid fileId,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
}
