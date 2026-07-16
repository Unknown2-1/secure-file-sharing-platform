namespace VaultShare.Domain.Shares;

public sealed class PublicDownloadSession
{
    private PublicDownloadSession() { }
    public PublicDownloadSession(Guid id, Guid shareId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    { Id = id; ShareId = shareId; TokenHash = tokenHash; CreatedAt = createdAt; ExpiresAt = expiresAt; }
    public Guid Id { get; private set; }
    public Guid ShareId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
}
