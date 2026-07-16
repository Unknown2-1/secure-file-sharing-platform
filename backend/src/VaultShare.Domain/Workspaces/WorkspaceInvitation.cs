namespace VaultShare.Domain.Workspaces;

public sealed class WorkspaceInvitation
{
    private WorkspaceInvitation()
    {
    }

    public WorkspaceInvitation(
        Guid id,
        Guid workspaceId,
        string normalizedEmail,
        WorkspaceRole role,
        string secretTokenHash,
        Guid invitedByUserId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invitation ID is required.", nameof(id));
        if (workspaceId == Guid.Empty) throw new ArgumentException("Workspace ID is required.", nameof(workspaceId));
        if (invitedByUserId == Guid.Empty) throw new ArgumentException("Inviter ID is required.", nameof(invitedByUserId));
        if (string.IsNullOrWhiteSpace(normalizedEmail) || normalizedEmail.Length > 254) throw new ArgumentException("Email is invalid.", nameof(normalizedEmail));
        if (string.IsNullOrWhiteSpace(secretTokenHash) || secretTokenHash.Length != 64) throw new ArgumentException("Token hash is invalid.", nameof(secretTokenHash));
        if (role == WorkspaceRole.Owner) throw new ArgumentException("Ownership cannot be granted by invitation.", nameof(role));
        if (expiresAt <= createdAt) throw new ArgumentException("Expiry must be after creation.", nameof(expiresAt));

        Id = id;
        WorkspaceId = workspaceId;
        NormalizedEmail = normalizedEmail;
        Role = role;
        SecretTokenHash = secretTokenHash;
        InvitedByUserId = invitedByUserId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string NormalizedEmail { get; private set; } = string.Empty;

    public WorkspaceRole Role { get; private set; }

    public string SecretTokenHash { get; private set; } = string.Empty;

    public Guid InvitedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public Guid? AcceptedByUserId { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public void Accept(Guid userId, DateTimeOffset acceptedAt)
    {
        if (AcceptedAt is not null || RevokedAt is not null || acceptedAt >= ExpiresAt)
        {
            throw new InvalidOperationException("Invitation is not active.");
        }

        AcceptedByUserId = userId;
        AcceptedAt = acceptedAt;
    }

    public void Revoke(DateTimeOffset revokedAt)
    {
        if (AcceptedAt is null && RevokedAt is null) RevokedAt = revokedAt;
    }
}
