namespace VaultShare.Domain.Files;

public enum InternalFilePermission { View, Download }

public sealed class InternalFileGrant
{
    private InternalFileGrant() { }
    public InternalFileGrant(Guid id, Guid storedFileId, Guid grantedToUserId, InternalFilePermission permission,
        Guid grantedByUserId, DateTimeOffset? expiresAt, DateTimeOffset createdAt)
    { Id = id; StoredFileId = storedFileId; GrantedToUserId = grantedToUserId; Permission = permission; GrantedByUserId = grantedByUserId; ExpiresAt = expiresAt; CreatedAt = createdAt; }
    public Guid Id { get; private set; }
    public Guid StoredFileId { get; private set; }
    public Guid GrantedToUserId { get; private set; }
    public InternalFilePermission Permission { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);
    public void Revoke(Guid actorId, DateTimeOffset now) { RevokedAt ??= now; RevokedByUserId ??= actorId; }
}
