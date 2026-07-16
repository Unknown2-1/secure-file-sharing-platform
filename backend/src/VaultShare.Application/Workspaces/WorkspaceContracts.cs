using System.Security.Claims;

namespace VaultShare.Application.Workspaces;

public sealed record WorkspaceSummary(Guid Id, string Name, string Role, DateTimeOffset CreatedAt);

public sealed record WorkspaceMemberSummary(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset JoinedAt);

public sealed record WorkspaceInvitationCreated(Guid Id, string SecretToken, DateTimeOffset ExpiresAt);

public enum WorkspaceOperationStatus
{
    Success,
    Forbidden,
    NotFound,
    Conflict,
    Invalid,
}

public sealed record WorkspaceOperationResult<T>(WorkspaceOperationStatus Status, string ErrorCode, T? Value)
{
    public static WorkspaceOperationResult<T> Success(T value) => new(WorkspaceOperationStatus.Success, string.Empty, value);

    public static WorkspaceOperationResult<T> Failure(WorkspaceOperationStatus status, string code) => new(status, code, default);
}

public interface IWorkspaceService
{
    Task<IReadOnlyList<WorkspaceSummary>> ListAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<WorkspaceSummary?> GetAsync(Guid workspaceId, ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<WorkspaceSummary> CreateAsync(string name, ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceMemberSummary>> ListMembersAsync(
        Guid workspaceId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<WorkspaceOperationResult<WorkspaceInvitationCreated>> InviteAsync(
        Guid workspaceId,
        string email,
        string role,
        int expiresInHours,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<WorkspaceOperationResult<bool>> AcceptInvitationAsync(
        Guid invitationId,
        string secretToken,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<WorkspaceOperationResult<bool>> ChangeMemberRoleAsync(
        Guid workspaceId,
        Guid targetUserId,
        string role,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<WorkspaceOperationResult<bool>> RemoveMemberAsync(
        Guid workspaceId,
        Guid targetUserId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task<WorkspaceOperationResult<bool>> DeleteAsync(Guid workspaceId, string idempotencyKey,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
}
