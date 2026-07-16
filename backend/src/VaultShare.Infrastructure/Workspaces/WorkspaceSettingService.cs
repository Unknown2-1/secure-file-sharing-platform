using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VaultShare.Application.Workspaces;
using VaultShare.Domain.Auditing;
using VaultShare.Domain.Workspaces;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Workspaces;

internal sealed class WorkspaceSettingService(VaultShareDbContext dbContext,
    UserManager<ApplicationUser> userManager, IConfiguration configuration) : IWorkspaceSettingService
{
    public async Task<WorkspaceSettingSummary?> GetAsync(Guid workspaceId, ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null || !await IsMemberAsync(workspaceId, user.Id, cancellationToken)) return null;
        var setting = await dbContext.WorkspaceSettings.AsNoTracking().SingleOrDefaultAsync(item =>
            item.WorkspaceId == workspaceId, cancellationToken);
        return setting is null
            ? new WorkspaceSettingSummary(workspaceId, ReadLong("WORKSPACE_DEFAULT_QUOTA", 10L * 1024 * 1024 * 1024),
                ReadInt("AUDIT_RETENTION_DAYS", 365), ReadGraceDays(), true, DateTimeOffset.MinValue)
            : ToSummary(setting);
    }

    public async Task<WorkspaceSettingSummary?> UpdateAsync(Guid workspaceId, long storageQuotaBytes,
        int auditRetentionDays, int deletedFileGraceDays, bool allowMemberPublicShares,
        ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return null;
        var owner = await dbContext.WorkspaceMembers.AsNoTracking().AnyAsync(member =>
            member.WorkspaceId == workspaceId && member.UserId == user.Id && member.RemovedAt == null &&
            member.Role == WorkspaceRole.Owner, cancellationToken);
        if (!owner) return null;
        var setting = await EnsureAsync(workspaceId, user.Id, cancellationToken);
        setting.Update(storageQuotaBytes, auditRetentionDays, deletedFileGraceDays,
            allowMemberPublicShares, DateTimeOffset.UtcNow, user.Id);
        dbContext.AuditEvents.Add(new AuditEvent(Guid.CreateVersion7(), workspaceId, user.Id,
            "WorkspaceSecuritySettingChanged", "WorkspaceSetting", workspaceId.ToString("D"),
            DateTimeOffset.UtcNow, "redacted", "VaultShare.Api", "workspace-setting", "Success", "{}"));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToSummary(setting);
    }

    private async Task<WorkspaceSetting> EnsureAsync(Guid workspaceId, Guid actorId,
        CancellationToken cancellationToken)
    {
        var setting = await dbContext.WorkspaceSettings.SingleOrDefaultAsync(item =>
            item.WorkspaceId == workspaceId, cancellationToken);
        if (setting is not null) return setting;
        if (!await dbContext.Workspaces.AnyAsync(item => item.Id == workspaceId && item.DeletedAt == null, cancellationToken))
            throw new InvalidOperationException("Workspace does not exist.");
        setting = new WorkspaceSetting(workspaceId, ReadLong("WORKSPACE_DEFAULT_QUOTA", 10L * 1024 * 1024 * 1024),
            ReadInt("AUDIT_RETENTION_DAYS", 365), ReadGraceDays(), true, DateTimeOffset.UtcNow, actorId);
        dbContext.WorkspaceSettings.Add(setting);
        await dbContext.SaveChangesAsync(cancellationToken);
        return setting;
    }

    private Task<bool> IsMemberAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.WorkspaceMembers.AsNoTracking().AnyAsync(member => member.WorkspaceId == workspaceId &&
            member.UserId == userId && member.RemovedAt == null, cancellationToken);

    private long ReadLong(string key, long fallback) => long.TryParse(configuration[key], out var value) && value > 0 ? value : fallback;
    private int ReadInt(string key, int fallback) => int.TryParse(configuration[key], out var value) && value > 0 ? value : fallback;
    private int ReadGraceDays() => TimeSpan.TryParse(configuration["DELETED_FILE_GRACE_PERIOD"], out var value)
        ? Math.Clamp((int)Math.Ceiling(value.TotalDays), 0, 365) : 30;
    private static WorkspaceSettingSummary ToSummary(WorkspaceSetting setting) => new(setting.WorkspaceId,
        setting.StorageQuotaBytes, setting.AuditRetentionDays, setting.DeletedFileGraceDays,
        setting.AllowMemberPublicShares, setting.UpdatedAt);
}
