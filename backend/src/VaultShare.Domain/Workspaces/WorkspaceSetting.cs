namespace VaultShare.Domain.Workspaces;

public sealed class WorkspaceSetting
{
    private WorkspaceSetting() { }

    public WorkspaceSetting(Guid workspaceId, long storageQuotaBytes, int auditRetentionDays,
        int deletedFileGraceDays, bool allowMemberPublicShares, DateTimeOffset updatedAt, Guid updatedByUserId)
    {
        WorkspaceId = workspaceId;
        Update(storageQuotaBytes, auditRetentionDays, deletedFileGraceDays, allowMemberPublicShares,
            updatedAt, updatedByUserId);
    }

    public Guid WorkspaceId { get; private set; }
    public long StorageQuotaBytes { get; private set; }
    public int AuditRetentionDays { get; private set; }
    public int DeletedFileGraceDays { get; private set; }
    public bool AllowMemberPublicShares { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public uint Version { get; private set; }

    public void Update(long storageQuotaBytes, int auditRetentionDays, int deletedFileGraceDays,
        bool allowMemberPublicShares, DateTimeOffset updatedAt, Guid updatedByUserId)
    {
        if (storageQuotaBytes is < 1_048_576 or > 1_125_899_906_842_624)
            throw new ArgumentOutOfRangeException(nameof(storageQuotaBytes));
        if (auditRetentionDays is < 30 or > 3650) throw new ArgumentOutOfRangeException(nameof(auditRetentionDays));
        if (deletedFileGraceDays is < 0 or > 365) throw new ArgumentOutOfRangeException(nameof(deletedFileGraceDays));
        StorageQuotaBytes = storageQuotaBytes;
        AuditRetentionDays = auditRetentionDays;
        DeletedFileGraceDays = deletedFileGraceDays;
        AllowMemberPublicShares = allowMemberPublicShares;
        UpdatedAt = updatedAt;
        UpdatedByUserId = updatedByUserId;
        Version++;
    }
}
