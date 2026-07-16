using System.Security.Claims;

namespace VaultShare.Application.Workspaces;

public sealed record WorkspaceSettingSummary(Guid WorkspaceId, long StorageQuotaBytes,
    int AuditRetentionDays, int DeletedFileGraceDays, bool AllowMemberPublicShares,
    DateTimeOffset UpdatedAt);

public interface IWorkspaceSettingService
{
    Task<WorkspaceSettingSummary?> GetAsync(Guid workspaceId, ClaimsPrincipal principal,
        CancellationToken cancellationToken);
    Task<WorkspaceSettingSummary?> UpdateAsync(Guid workspaceId, long storageQuotaBytes,
        int auditRetentionDays, int deletedFileGraceDays, bool allowMemberPublicShares,
        ClaimsPrincipal principal, CancellationToken cancellationToken);
}
