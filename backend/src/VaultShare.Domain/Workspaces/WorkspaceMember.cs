namespace VaultShare.Domain.Workspaces;

public sealed class WorkspaceMember
{
    private WorkspaceMember()
    {
    }

    public WorkspaceMember(Guid workspaceId, Guid userId, WorkspaceRole role, DateTimeOffset joinedAt)
    {
        if (workspaceId == Guid.Empty) throw new ArgumentException("Workspace ID is required.", nameof(workspaceId));
        if (userId == Guid.Empty) throw new ArgumentException("User ID is required.", nameof(userId));

        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid UserId { get; private set; }

    public WorkspaceRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public void ChangeRole(WorkspaceRole role)
    {
        Role = role;
    }

    public void Remove(DateTimeOffset removedAt)
    {
        RemovedAt ??= removedAt;
    }

    public void Restore(WorkspaceRole role, DateTimeOffset joinedAt)
    {
        Role = role;
        JoinedAt = joinedAt;
        RemovedAt = null;
    }
}
