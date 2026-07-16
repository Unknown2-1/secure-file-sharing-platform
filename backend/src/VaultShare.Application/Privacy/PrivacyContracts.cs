using System.Security.Claims;

namespace VaultShare.Application.Privacy;

public sealed record ExportedMembership(Guid WorkspaceId, string WorkspaceName, string Role, DateTimeOffset JoinedAt);
public sealed record ExportedFile(Guid Id, Guid WorkspaceId, string Filename, long Size, string Status, DateTimeOffset CreatedAt, DateTimeOffset? DeletedAt);
public sealed record ExportedShare(Guid Id, Guid WorkspaceId, string Name, DateTimeOffset ExpiresAt, bool IsRevoked, int DownloadCount);
public sealed record ExportedAudit(Guid Id, Guid? WorkspaceId, string Action, string TargetType, string? TargetId, DateTimeOffset Timestamp, string Result);
public sealed record UserDataExport(Guid UserId, string Email, string DisplayName, DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt, IReadOnlyList<ExportedMembership> Memberships,
    IReadOnlyList<ExportedFile> Files, IReadOnlyList<ExportedShare> Shares,
    IReadOnlyList<ExportedAudit> AuditEvents, DateTimeOffset ExportedAt);

public interface IUserDataExportService
{
    Task<UserDataExport?> ExportAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
