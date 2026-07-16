namespace VaultShare.Domain.Shares;

public sealed class Share
{
    private Share() { }

    public Share(Guid id, Guid workspaceId, Guid createdByUserId, string publicIdentifier,
        string secretTokenHash, string creationIdempotencyKeyHash, string name, string? description,
        DateTimeOffset? startsAt, DateTimeOffset expiresAt, int? maximumDownloads, bool isOneTime,
        bool allowPreview, DateTimeOffset createdAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        CreatedByUserId = createdByUserId;
        PublicIdentifier = publicIdentifier;
        SecretTokenHash = secretTokenHash;
        CreationIdempotencyKeyHash = creationIdempotencyKeyHash;
        Name = name;
        Description = description;
        StartsAt = startsAt;
        ExpiresAt = expiresAt;
        MaximumDownloads = isOneTime ? 1 : maximumDownloads;
        IsOneTime = isOneTime;
        AllowPreview = allowPreview;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string PublicIdentifier { get; private set; } = string.Empty;
    public string SecretTokenHash { get; private set; } = string.Empty;
    public string CreationIdempotencyKeyHash { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? PasswordHash { get; private set; }
    public DateTimeOffset? StartsAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public int? MaximumDownloads { get; private set; }
    public int DownloadCount { get; private set; }
    public bool IsOneTime { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public bool AllowPreview { get; private set; }
    public bool RequireEmailVerification { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastAccessedAt { get; private set; }
    public DateTimeOffset? ExpirationNotifiedAt { get; private set; }
    public uint Version { get; private set; }

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public bool CanAccess(DateTimeOffset now) => !IsRevoked && (StartsAt is null || StartsAt <= now) && ExpiresAt > now;

    public bool TryReserveDownload(DateTimeOffset now)
    {
        if (!CanAccess(now) || (MaximumDownloads is int maximum && DownloadCount >= maximum)) return false;
        DownloadCount++;
        LastAccessedAt = now;
        Version++;
        return true;
    }

    public void Revoke(Guid actorUserId, DateTimeOffset now)
    {
        IsRevoked = true;
        RevokedAt ??= now;
        RevokedBy ??= actorUserId;
        Version++;
    }

    public void MarkExpirationNotified(DateTimeOffset now) => ExpirationNotifiedAt ??= now;
}
