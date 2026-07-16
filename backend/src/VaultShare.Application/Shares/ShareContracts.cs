using System.Security.Claims;
using VaultShare.Application.Encryption;

namespace VaultShare.Application.Shares;

public sealed record CreateShareCommand(Guid WorkspaceId, IReadOnlyList<Guid> FileIds, string Name,
    string? Description, string? Password, DateTimeOffset? StartsAt, DateTimeOffset ExpiresAt,
    int? MaximumDownloads, bool IsOneTime, bool AllowPreview, string IdempotencyKey);
public sealed record ShareCreated(Guid Id, string PublicIdentifier, string SecretToken, DateTimeOffset ExpiresAt);
public sealed record ShareSummary(Guid Id, Guid WorkspaceId, string Name, string? Description,
    DateTimeOffset? StartsAt, DateTimeOffset ExpiresAt, int? MaximumDownloads, int DownloadCount,
    bool IsOneTime, bool IsRevoked, bool IsPasswordProtected, bool AllowPreview, DateTimeOffset CreatedAt,
    DateTimeOffset? LastAccessedAt, IReadOnlyList<SharedFileSummary> Files);
public sealed record SharedFileSummary(Guid Id, string Filename, long Size, string? DetectedMimeType);
public sealed record PublicShareSession(string SessionToken, DateTimeOffset ExpiresAt, string Name,
    string? Description, bool AllowPreview, IReadOnlyList<SharedFileSummary> Files);
public enum ShareOperationStatus { Success, Invalid, Forbidden, NotFound, Conflict, AccessDenied }
public sealed record ShareOperationResult<T>(ShareOperationStatus Status, T? Value, string ErrorCode)
{
    public static ShareOperationResult<T> Success(T value) => new(ShareOperationStatus.Success, value, string.Empty);
    public static ShareOperationResult<T> Failure(ShareOperationStatus status, string code) => new(status, default, code);
}

public interface ISecureTokenGenerator
{
    string Generate(int byteLength);
    string Hash(string token);
    bool Verify(string token, string expectedHash);
}

public interface IShareService
{
    Task<ShareOperationResult<ShareCreated>> CreateAsync(CreateShareCommand command,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<ShareOperationResult<IReadOnlyList<ShareSummary>>> ListAsync(Guid workspaceId,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<ShareOperationResult<bool>> RevokeAsync(Guid shareId, string idempotencyKey,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<ShareOperationResult<PublicShareSession>> CreatePublicSessionAsync(string publicIdentifier,
        string secretToken, string? password, CancellationToken cancellationToken);
}

public sealed record DownloadReservation(Guid DownloadId, Guid FileId, string Filename, long Size,
    string DetectedMimeType, string ObjectKey, FileEncryptionResult EncryptionMetadata);
public sealed record PreviewReservation(Guid FileId, string Filename, long Size,
    string DetectedMimeType, string ObjectKey, FileEncryptionResult EncryptionMetadata);

public interface IDownloadService
{
    Task<ShareOperationResult<DownloadReservation>> ReserveAsync(Guid fileId, string sessionToken,
        CancellationToken cancellationToken);
    Task CompleteAsync(Guid downloadId, bool succeeded, CancellationToken cancellationToken);
    Task<ShareOperationResult<PreviewReservation>> AuthorizePreviewAsync(Guid fileId, string sessionToken,
        CancellationToken cancellationToken);
}
