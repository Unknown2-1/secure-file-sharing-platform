namespace VaultShare.Domain.Files;

public sealed class FileUpload
{
    private FileUpload() { }

    public FileUpload(Guid id, Guid storedFileId, Guid workspaceId, Guid userId, string temporaryPath,
        long expectedSize, string idempotencyKey, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = id;
        StoredFileId = storedFileId;
        WorkspaceId = workspaceId;
        UserId = userId;
        TemporaryPath = temporaryPath;
        ExpectedSize = expectedSize;
        IdempotencyKey = idempotencyKey;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid StoredFileId { get; private set; }
    public StoredFile StoredFile { get; private set; } = null!;
    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }
    public string TemporaryPath { get; private set; } = string.Empty;
    public long ExpectedSize { get; private set; }
    public long UploadOffset { get; private set; }
    public UploadStatus Status { get; private set; } = UploadStatus.Pending;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public uint Version { get; private set; }

    public void Advance(long expectedOffset, long bytesWritten, DateTimeOffset now)
    {
        if (Status is UploadStatus.Uploaded or UploadStatus.Processing or UploadStatus.Completed)
            throw new InvalidOperationException("Upload is already finalized.");
        if (expectedOffset != UploadOffset) throw new InvalidOperationException("Upload offset does not match.");
        if (bytesWritten <= 0 || UploadOffset + bytesWritten > ExpectedSize)
            throw new InvalidOperationException("Chunk size is invalid.");
        UploadOffset += bytesWritten;
        Status = UploadStatus.Uploading;
        UpdatedAt = now;
        Version++;
    }

    public void FinalizeUpload(DateTimeOffset now)
    {
        if (Status == UploadStatus.Uploaded) return;
        if (UploadOffset != ExpectedSize) throw new InvalidOperationException("Upload is incomplete.");
        Status = UploadStatus.Uploaded;
        UpdatedAt = now;
        Version++;
    }

    public void Abandon(DateTimeOffset now)
    {
        if (Status is UploadStatus.Uploaded or UploadStatus.Processing or UploadStatus.Completed) return;
        Status = UploadStatus.Abandoned;
        UpdatedAt = now;
        Version++;
    }

    public void StartProcessing(DateTimeOffset now)
    {
        if (Status != UploadStatus.Uploaded) throw new InvalidOperationException("Only uploaded files can be processed.");
        Status = UploadStatus.Processing;
        UpdatedAt = now;
        Version++;
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status != UploadStatus.Processing) throw new InvalidOperationException("Upload is not processing.");
        Status = UploadStatus.Completed;
        UpdatedAt = now;
        Version++;
    }

    public void Fail(DateTimeOffset now)
    {
        Status = UploadStatus.Failed;
        UpdatedAt = now;
        Version++;
    }
}
