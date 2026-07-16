namespace VaultShare.Infrastructure.Identity;

public sealed class UserSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string UserAgent { get; set; } = string.Empty;

    public string IpAddressHash { get; set; } = string.Empty;
}
