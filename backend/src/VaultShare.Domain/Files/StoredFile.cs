namespace VaultShare.Domain.Files;

public sealed class StoredFile
{
    private StoredFile() { }

    public StoredFile(Guid id, Guid workspaceId, Guid ownerUserId, string originalFilename,
        string safeDisplayFilename, string storedObjectKey, long fileSize, string clientMimeType,
        DateTimeOffset createdAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        OwnerUserId = ownerUserId;
        OriginalFilename = originalFilename;
        SafeDisplayFilename = safeDisplayFilename;
        StoredObjectKey = storedObjectKey;
        FileSize = fileSize;
        ClientMimeType = clientMimeType;
        CreatedAt = createdAt;
        CreatedBy = ownerUserId;
    }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string OriginalFilename { get; private set; } = string.Empty;
    public string SafeDisplayFilename { get; private set; } = string.Empty;
    public string StoredObjectKey { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public string? DetectedMimeType { get; private set; }
    public string ClientMimeType { get; private set; } = string.Empty;
    public string? Sha256Hash { get; private set; }
    public UploadStatus UploadStatus { get; private set; } = UploadStatus.Pending;
    public MalwareScanStatus MalwareScanStatus { get; private set; } = MalwareScanStatus.Pending;
    public EncryptionStatus EncryptionStatus { get; private set; } = EncryptionStatus.Pending;
    public AvailabilityStatus AvailabilityStatus { get; private set; } = AvailabilityStatus.Processing;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public DateTimeOffset? PurgedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public int MetadataVersion { get; private set; } = 1;
    public Guid? EncryptionMetadataId { get; private set; }

    public void MarkUploaded() => UploadStatus = UploadStatus.Uploaded;

    public void MarkProcessing() => UploadStatus = UploadStatus.Processing;

    public void MarkValidated(string detectedMimeType, string sha256Hash)
    {
        DetectedMimeType = detectedMimeType;
        Sha256Hash = sha256Hash;
    }

    public void MarkScanning() => MalwareScanStatus = MalwareScanStatus.Scanning;

    public void MarkScanClean() => MalwareScanStatus = MalwareScanStatus.Clean;

    public void MarkScanFailure(MalwareScanStatus status, bool quarantine)
    {
        if (status is MalwareScanStatus.Clean or MalwareScanStatus.Pending or MalwareScanStatus.Scanning)
            throw new ArgumentOutOfRangeException(nameof(status));
        MalwareScanStatus = status;
        AvailabilityStatus = quarantine ? AvailabilityStatus.Quarantined : AvailabilityStatus.Failed;
        UploadStatus = UploadStatus.Failed;
    }

    public void MarkEncryptionStarted() => EncryptionStatus = EncryptionStatus.Encrypting;

    public void MarkEncrypted(Guid metadataId)
    {
        EncryptionMetadataId = metadataId;
        EncryptionStatus = EncryptionStatus.Encrypted;
    }

    public void MarkAvailable(DateTimeOffset processedAt)
    {
        if (MalwareScanStatus != MalwareScanStatus.Clean || EncryptionStatus != EncryptionStatus.Encrypted)
            throw new InvalidOperationException("A file must be clean and encrypted before it is available.");
        UploadStatus = UploadStatus.Completed;
        AvailabilityStatus = AvailabilityStatus.Available;
        ProcessedAt = processedAt;
    }

    public void MarkProcessingFailed()
    {
        UploadStatus = UploadStatus.Failed;
        AvailabilityStatus = AvailabilityStatus.Failed;
        if (EncryptionStatus == EncryptionStatus.Encrypting) EncryptionStatus = EncryptionStatus.Failed;
    }

    public void MarkEncryptionStorageFailed()
    {
        EncryptionMetadataId = null;
        EncryptionStatus = EncryptionStatus.Failed;
        UploadStatus = UploadStatus.Failed;
        AvailabilityStatus = AvailabilityStatus.Failed;
    }

    public void SoftDelete(DateTimeOffset deletedAt)
    {
        if (PurgedAt is not null) throw new InvalidOperationException("Purged files cannot be deleted again.");
        DeletedAt ??= deletedAt;
        AvailabilityStatus = AvailabilityStatus.Deleted;
    }

    public void Restore(DateTimeOffset restoreDeadline, DateTimeOffset now)
    {
        if (DeletedAt is null || PurgedAt is not null || now > restoreDeadline)
            throw new InvalidOperationException("File cannot be restored.");
        DeletedAt = null;
        AvailabilityStatus = MalwareScanStatus == MalwareScanStatus.Clean && EncryptionStatus == EncryptionStatus.Encrypted
            ? AvailabilityStatus.Available
            : AvailabilityStatus.Failed;
    }

    public void MarkPurged(DateTimeOffset purgedAt)
    {
        if (DeletedAt is null) throw new InvalidOperationException("File must be deleted before purge.");
        EncryptionMetadataId = null;
        PurgedAt ??= purgedAt;
        AvailabilityStatus = AvailabilityStatus.Purged;
    }

    public void MarkUploadAbandoned()
    {
        if (UploadStatus is UploadStatus.Completed or UploadStatus.Processing) return;
        UploadStatus = UploadStatus.Abandoned;
        AvailabilityStatus = AvailabilityStatus.Failed;
    }
}
