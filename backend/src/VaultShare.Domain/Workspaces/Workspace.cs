namespace VaultShare.Domain.Workspaces;

public sealed class Workspace
{
    private Workspace()
    {
    }

    public Workspace(Guid id, string name, Guid createdByUserId, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Workspace ID is required.", nameof(id));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("Creator ID is required.", nameof(createdByUserId));
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120) throw new ArgumentException("Workspace name is invalid.", nameof(name));

        Id = id;
        Name = name.Trim();
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public uint Version { get; private set; }

    public void Delete(DateTimeOffset deletedAt)
    {
        DeletedAt ??= deletedAt;
    }
}
