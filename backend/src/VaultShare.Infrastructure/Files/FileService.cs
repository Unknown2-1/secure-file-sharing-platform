using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VaultShare.Application.Files;
using VaultShare.Application.Storage;
using VaultShare.Domain.Files;
using VaultShare.Domain.Workspaces;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Files;

internal sealed class FileService(
    VaultShareDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IObjectStorage objectStorage,
    IConfiguration configuration) : IFileService
{
    public async Task<FileOperationResult<FilePage>> ListAsync(Guid workspaceId, string? search, string? status,
        int page, int pageSize, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var access = await GetAccessAsync(workspaceId, principal, cancellationToken);
        if (access is null) return FileOperationResult<FilePage>.Failure(FileOperationStatus.Forbidden, "file.forbidden");
        if (page < 1 || pageSize is < 1 or > 100 || search?.Length > 120)
            return FileOperationResult<FilePage>.Failure(FileOperationStatus.Invalid, "file.invalid_query");

        var now = DateTimeOffset.UtcNow;
        var query = dbContext.StoredFiles.AsNoTracking().Where(file => file.WorkspaceId == workspaceId && file.PurgedAt == null);
        if (access.Value.Role is WorkspaceRole.Member or WorkspaceRole.Viewer)
        {
            var userId = access.Value.UserId;
            query = query.Where(file => file.OwnerUserId == userId || dbContext.InternalFileGrants.Any(grant =>
                grant.StoredFileId == file.Id && grant.GrantedToUserId == userId && grant.RevokedAt == null &&
                (grant.ExpiresAt == null || grant.ExpiresAt > now)));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(file => file.SafeDisplayFilename.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<AvailabilityStatus>(status, true, out var parsed))
                return FileOperationResult<FilePage>.Failure(FileOperationStatus.Invalid, "file.invalid_status");
            query = query.Where(file => file.AvailabilityStatus == parsed);
        }

        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query.OrderByDescending(file => file.CreatedAt).ThenBy(file => file.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = rows.Select(ToSummary).ToList();
        return FileOperationResult<FilePage>.Success(new(items, page, pageSize, total));
    }

    public async Task<FileOperationResult<FileSummary>> GetAsync(Guid fileId, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var file = await dbContext.StoredFiles.AsNoTracking().SingleOrDefaultAsync(item => item.Id == fileId && item.PurgedAt == null, cancellationToken);
        if (file is null) return FileOperationResult<FileSummary>.Failure(FileOperationStatus.NotFound, "file.not_found");
        var access = await GetAccessAsync(file.WorkspaceId, principal, cancellationToken);
        if (access is null)
            return FileOperationResult<FileSummary>.Failure(FileOperationStatus.Forbidden, "file.forbidden");
        if (access.Value.Role is WorkspaceRole.Member or WorkspaceRole.Viewer && file.OwnerUserId != access.Value.UserId)
        {
            var now = DateTimeOffset.UtcNow;
            var hasGrant = await dbContext.InternalFileGrants.AsNoTracking().AnyAsync(grant =>
                grant.StoredFileId == file.Id && grant.GrantedToUserId == access.Value.UserId &&
                grant.RevokedAt == null && (grant.ExpiresAt == null || grant.ExpiresAt > now), cancellationToken);
            if (!hasGrant) return FileOperationResult<FileSummary>.Failure(FileOperationStatus.Forbidden, "file.forbidden");
        }
        return FileOperationResult<FileSummary>.Success(ToSummary(file));
    }

    public Task<FileOperationResult<bool>> DeleteAsync(Guid fileId, string idempotencyKey,
        ClaimsPrincipal principal, CancellationToken cancellationToken) => MutateAsync(fileId, idempotencyKey, principal,
            (file, now) => file.SoftDelete(now), cancellationToken);

    public Task<FileOperationResult<bool>> RestoreAsync(Guid fileId, string idempotencyKey,
        ClaimsPrincipal principal, CancellationToken cancellationToken) => MutateAsync(fileId, idempotencyKey, principal,
            (file, now) => file.Restore(file.DeletedAt?.Add(GetGracePeriod()) ?? DateTimeOffset.MinValue, now), cancellationToken);

    public async Task<FileOperationResult<bool>> PurgeAsync(Guid fileId, string idempotencyKey,
        ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!ValidKey(idempotencyKey)) return FileOperationResult<bool>.Failure(FileOperationStatus.Invalid, "file.invalid_idempotency_key");
        var file = await dbContext.StoredFiles.SingleOrDefaultAsync(item => item.Id == fileId, cancellationToken);
        if (file is null) return FileOperationResult<bool>.Failure(FileOperationStatus.NotFound, "file.not_found");
        var access = await GetAccessAsync(file.WorkspaceId, principal, cancellationToken);
        if (!CanManage(access, file)) return FileOperationResult<bool>.Failure(FileOperationStatus.Forbidden, "file.forbidden");
        if (file.PurgedAt is not null) return FileOperationResult<bool>.Success(true);
        if (file.DeletedAt is null || DateTimeOffset.UtcNow < file.DeletedAt.Value.Add(GetGracePeriod()))
            return FileOperationResult<bool>.Failure(FileOperationStatus.Conflict, "file.purge_grace_period");

        await objectStorage.DeleteAsync(file.StoredObjectKey, cancellationToken);
        if (file.EncryptionMetadataId is Guid metadataId)
        {
            var metadata = await dbContext.FileEncryptionMetadata.SingleOrDefaultAsync(item => item.Id == metadataId, cancellationToken);
            if (metadata is not null) dbContext.FileEncryptionMetadata.Remove(metadata);
        }
        file.MarkPurged(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return FileOperationResult<bool>.Success(true);
    }

    private async Task<FileOperationResult<bool>> MutateAsync(Guid fileId, string idempotencyKey,
        ClaimsPrincipal principal, Action<StoredFile, DateTimeOffset> mutation, CancellationToken cancellationToken)
    {
        if (!ValidKey(idempotencyKey)) return FileOperationResult<bool>.Failure(FileOperationStatus.Invalid, "file.invalid_idempotency_key");
        var file = await dbContext.StoredFiles.SingleOrDefaultAsync(item => item.Id == fileId, cancellationToken);
        if (file is null) return FileOperationResult<bool>.Failure(FileOperationStatus.NotFound, "file.not_found");
        var access = await GetAccessAsync(file.WorkspaceId, principal, cancellationToken);
        if (!CanManage(access, file)) return FileOperationResult<bool>.Failure(FileOperationStatus.Forbidden, "file.forbidden");
        try { mutation(file, DateTimeOffset.UtcNow); }
        catch (InvalidOperationException) { return FileOperationResult<bool>.Failure(FileOperationStatus.Conflict, "file.invalid_state"); }
        await dbContext.SaveChangesAsync(cancellationToken);
        return FileOperationResult<bool>.Success(true);
    }

    private async Task<(Guid UserId, WorkspaceRole Role)?> GetAccessAsync(Guid workspaceId,
        ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return null;
        var membership = await dbContext.WorkspaceMembers.AsNoTracking().SingleOrDefaultAsync(
            item => item.WorkspaceId == workspaceId && item.UserId == user.Id && item.RemovedAt == null, cancellationToken);
        return membership is null ? null : (user.Id, membership.Role);
    }

    private static bool CanManage((Guid UserId, WorkspaceRole Role)? access, StoredFile file) =>
        access is { } value && (value.Role is WorkspaceRole.Owner or WorkspaceRole.Admin || file.OwnerUserId == value.UserId);

    private TimeSpan GetGracePeriod() => TimeSpan.TryParse(configuration["DELETED_FILE_GRACE_PERIOD"], out var period)
        ? period : TimeSpan.FromDays(30);
    private static bool ValidKey(string value) => value.Length is >= 8 and <= 128 && value.All(character => character is >= '!' and <= '~');
    private static FileSummary ToSummary(StoredFile file) => new(file.Id, file.WorkspaceId, file.OwnerUserId,
        file.SafeDisplayFilename, file.FileSize, file.DetectedMimeType, file.UploadStatus.ToString(),
        file.MalwareScanStatus.ToString(), file.EncryptionStatus.ToString(), file.AvailabilityStatus.ToString(),
        file.CreatedAt, file.ProcessedAt, file.DeletedAt);
}
