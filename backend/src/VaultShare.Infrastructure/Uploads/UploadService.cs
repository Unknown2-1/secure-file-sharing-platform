using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using VaultShare.Application.Uploads;
using VaultShare.Domain.Files;
using VaultShare.Domain.Workspaces;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Uploads;

internal sealed class UploadService(
    VaultShareDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    IHostEnvironment environment) : IUploadService
{
    private const long DefaultMaximumUploadSize = 1_073_741_824;
    private const long DefaultWorkspaceQuota = 5_368_709_120;
    private const long MaximumChunkSize = 8 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".bin",
    };

    public async Task<UploadOperationResult<UploadSessionInfo>> GetAsync(
        Guid uploadId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        var upload = await dbContext.FileUploads.AsNoTracking().Include(candidate => candidate.StoredFile)
            .SingleOrDefaultAsync(candidate => candidate.Id == uploadId, cancellationToken);
        if (upload is null) return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.NotFound, "upload.not_found");
        return user is not null && upload.UserId == user.Id
            ? UploadOperationResult<UploadSessionInfo>.Success(ToInfo(upload))
            : UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Forbidden, "upload.forbidden");
    }

    public async Task<UploadOperationResult<UploadSessionInfo>> CreateAsync(
        CreateUploadCommand command,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Forbidden, "upload.forbidden");

        if (!ValidIdempotencyKey(command.IdempotencyKey) || command.FileSize <= 0 ||
            command.Filename.Length is 0 or > 255 || command.Filename.Contains('\0') ||
            string.IsNullOrWhiteSpace(command.ClientMimeType) || command.ClientMimeType.Length > 127)
            return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Invalid, "upload.invalid_metadata");

        var maximumUploadSize = GetLong("MAX_UPLOAD_SIZE", DefaultMaximumUploadSize);
        if (command.FileSize > maximumUploadSize)
            return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.TooLarge, "upload.too_large");

        var existing = await dbContext.FileUploads
            .Include(upload => upload.StoredFile)
            .SingleOrDefaultAsync(upload => upload.UserId == user.Id && upload.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (existing is not null) return UploadOperationResult<UploadSessionInfo>.Success(ToInfo(existing));

        var membership = await dbContext.WorkspaceMembers.AsNoTracking().SingleOrDefaultAsync(
            member => member.WorkspaceId == command.WorkspaceId && member.UserId == user.Id && member.RemovedAt == null,
            cancellationToken);
        if (membership is null || membership.Role == WorkspaceRole.Viewer)
            return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Forbidden, "upload.forbidden");

        var usedBytes = await dbContext.StoredFiles
            .Where(file => file.WorkspaceId == command.WorkspaceId && file.PurgedAt == null &&
                           file.UploadStatus != UploadStatus.Abandoned && file.UploadStatus != UploadStatus.Failed)
            .SumAsync(file => (long?)file.FileSize, cancellationToken) ?? 0;
        var workspaceQuota = await dbContext.WorkspaceSettings.AsNoTracking()
            .Where(setting => setting.WorkspaceId == command.WorkspaceId)
            .Select(setting => (long?)setting.StorageQuotaBytes).SingleOrDefaultAsync(cancellationToken)
            ?? GetLong("WORKSPACE_DEFAULT_QUOTA", DefaultWorkspaceQuota);
        if (usedBytes > workspaceQuota - command.FileSize)
            return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.QuotaExceeded, "upload.quota_exceeded");

        var safeFilename = SanitizeFilename(command.Filename);
        var extension = Path.GetExtension(safeFilename);
        if (!AllowedExtensions.Contains(extension))
            return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Invalid, "upload.extension_not_allowed");

        var now = DateTimeOffset.UtcNow;
        var storedFileId = Guid.CreateVersion7();
        var uploadId = Guid.CreateVersion7();
        var temporaryRoot = environment.IsEnvironment("Testing")
            ? Path.Combine(Path.GetTempPath(), "vaultshare-tests")
            : configuration["TEMP_UPLOAD_PATH"] ?? throw new InvalidOperationException("TEMP_UPLOAD_PATH is required.");
        Directory.CreateDirectory(temporaryRoot);
        var temporaryPath = Path.Combine(temporaryRoot, $"{uploadId:N}.upload");
        await using (var placeholder = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await placeholder.FlushAsync(cancellationToken);
        }

        var storedFile = new StoredFile(storedFileId, command.WorkspaceId, user.Id, command.Filename,
            safeFilename, $"encrypted/{command.WorkspaceId:D}/{Guid.NewGuid():N}", command.FileSize,
            command.ClientMimeType.Trim(), now);
        var upload = new FileUpload(uploadId, storedFileId, command.WorkspaceId, user.Id, temporaryPath,
            command.FileSize, command.IdempotencyKey, now, now.AddHours(24));
        dbContext.StoredFiles.Add(storedFile);
        dbContext.FileUploads.Add(upload);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }

        return UploadOperationResult<UploadSessionInfo>.Success(ToInfo(upload, storedFile));
    }

    public async Task<UploadOperationResult<UploadSessionInfo>> AppendChunkAsync(
        Guid uploadId,
        long offset,
        long contentLength,
        Stream content,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        var upload = await dbContext.FileUploads.Include(candidate => candidate.StoredFile)
            .SingleOrDefaultAsync(candidate => candidate.Id == uploadId, cancellationToken);
        if (upload is null) return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.NotFound, "upload.not_found");
        if (user is null || upload.UserId != user.Id)
            return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Forbidden, "upload.forbidden");
        if (offset != upload.UploadOffset)
            return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Conflict, "upload.offset_mismatch");
        if (contentLength <= 0 || contentLength > MaximumChunkSize || offset > upload.ExpectedSize - contentLength)
            return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Invalid, "upload.invalid_chunk");

        try
        {
            await using var destination = new FileStream(upload.TemporaryPath, FileMode.Open, FileAccess.Write,
                FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough);
            if (destination.Length != offset)
                return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Conflict, "upload.offset_mismatch");
            destination.Position = offset;
            await CopyExactlyAsync(content, destination, contentLength, cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }
        catch (EndOfStreamException)
        {
            await TruncateAsync(upload.TemporaryPath, offset, cancellationToken);
            return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Invalid, "upload.incomplete_chunk");
        }
        catch (IOException)
        {
            return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Conflict, "upload.concurrent_write");
        }

        upload.Advance(offset, contentLength, DateTimeOffset.UtcNow);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await TruncateAsync(upload.TemporaryPath, offset, cancellationToken);
            return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Conflict, "upload.concurrent_write");
        }
        return UploadOperationResult<UploadSessionInfo>.Success(ToInfo(upload));
    }

    public async Task<UploadOperationResult<UploadSessionInfo>> FinalizeAsync(
        Guid uploadId,
        string idempotencyKey,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (!ValidIdempotencyKey(idempotencyKey))
            return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Invalid, "upload.invalid_idempotency_key");
        var user = await userManager.GetUserAsync(principal);
        var upload = await dbContext.FileUploads.Include(candidate => candidate.StoredFile)
            .SingleOrDefaultAsync(candidate => candidate.Id == uploadId, cancellationToken);
        if (upload is null) return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.NotFound, "upload.not_found");
        if (user is null || upload.UserId != user.Id)
            return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Forbidden, "upload.forbidden");
        if (upload.UploadOffset != upload.ExpectedSize)
            return UploadOperationResult<UploadSessionInfo>.Failure(UploadOperationStatus.Conflict, "upload.incomplete");

        upload.FinalizeUpload(DateTimeOffset.UtcNow);
        upload.StoredFile.MarkUploaded();
        await dbContext.SaveChangesAsync(cancellationToken);
        return UploadOperationResult<UploadSessionInfo>.Success(ToInfo(upload));
    }

    public async Task<UploadOperationResult<bool>> CancelAsync(
        Guid uploadId,
        string idempotencyKey,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (!ValidIdempotencyKey(idempotencyKey))
            return UploadOperationResult<bool>.Failure(UploadOperationStatus.Invalid, "upload.invalid_idempotency_key");
        var user = await userManager.GetUserAsync(principal);
        var upload = await dbContext.FileUploads.Include(candidate => candidate.StoredFile)
            .SingleOrDefaultAsync(candidate => candidate.Id == uploadId, cancellationToken);
        if (upload is null) return UploadOperationResult<bool>.Failure(UploadOperationStatus.NotFound, "upload.not_found");
        if (user is null || upload.UserId != user.Id)
            return UploadOperationResult<bool>.Failure(UploadOperationStatus.Forbidden, "upload.forbidden");
        if (upload.Status is UploadStatus.Uploaded or UploadStatus.Processing or UploadStatus.Completed)
            return UploadOperationResult<bool>.Failure(UploadOperationStatus.Conflict, "upload.already_finalized");

        upload.Abandon(DateTimeOffset.UtcNow);
        upload.StoredFile.MarkUploadAbandoned();
        await dbContext.SaveChangesAsync(cancellationToken);
        try
        {
            File.Delete(upload.TemporaryPath);
        }
        catch (IOException)
        {
            return UploadOperationResult<bool>.Failure(UploadOperationStatus.Conflict, "upload.cleanup_pending");
        }
        return UploadOperationResult<bool>.Success(true);
    }

    private static async Task CopyExactlyAsync(Stream source, Stream destination, long length, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        var remaining = length;
        while (remaining > 0)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken);
            if (read == 0) throw new EndOfStreamException();
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }
    }

    private static async Task TruncateAsync(string path, long length, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous);
        file.SetLength(length);
        await file.FlushAsync(cancellationToken);
    }

    private static UploadSessionInfo ToInfo(FileUpload upload, StoredFile? file = null)
    {
        file ??= upload.StoredFile;
        return new(upload.Id, upload.StoredFileId, upload.WorkspaceId, file.SafeDisplayFilename,
            upload.ExpectedSize, upload.UploadOffset, upload.Status.ToString(), upload.ExpiresAt);
    }

    private long GetLong(string key, long fallback) =>
        long.TryParse(configuration[key], out var value) && value > 0 ? value : fallback;

    private static bool ValidIdempotencyKey(string value) => value.Length is >= 8 and <= 128 && value.All(character => character is >= '!' and <= '~');

    private static string SanitizeFilename(string filename)
    {
        var leaf = filename.Replace('\\', '/').Split('/').Last();
        var sanitized = string.Concat(leaf.Select(character => char.IsControl(character) ? '-' : character));
        sanitized = sanitized.Trim().Trim('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "file" : sanitized;
    }
}
