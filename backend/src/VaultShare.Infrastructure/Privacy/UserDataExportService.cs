using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VaultShare.Application.Privacy;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Privacy;

internal sealed class UserDataExportService(VaultShareDbContext dbContext,
    UserManager<ApplicationUser> userManager) : IUserDataExportService
{
    public async Task<UserDataExport?> ExportAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return null;
        var memberships = await (from member in dbContext.WorkspaceMembers.AsNoTracking()
                                 join workspace in dbContext.Workspaces.AsNoTracking() on member.WorkspaceId equals workspace.Id
                                 where member.UserId == user.Id && member.RemovedAt == null
                                 orderby member.JoinedAt
                                 select new ExportedMembership(workspace.Id, workspace.Name, member.Role.ToString(), member.JoinedAt))
            .ToListAsync(cancellationToken);
        var files = await dbContext.StoredFiles.AsNoTracking().Where(file => file.OwnerUserId == user.Id)
            .OrderBy(file => file.CreatedAt).Select(file => new ExportedFile(file.Id, file.WorkspaceId,
                file.SafeDisplayFilename, file.FileSize, file.AvailabilityStatus.ToString(), file.CreatedAt, file.DeletedAt))
            .ToListAsync(cancellationToken);
        var shares = await dbContext.Shares.AsNoTracking().Where(share => share.CreatedByUserId == user.Id)
            .OrderBy(share => share.CreatedAt).Select(share => new ExportedShare(share.Id, share.WorkspaceId,
                share.Name, share.ExpiresAt, share.IsRevoked, share.DownloadCount)).ToListAsync(cancellationToken);
        var audits = await dbContext.AuditEvents.AsNoTracking().Where(item => item.ActorUserId == user.Id)
            .OrderBy(item => item.Timestamp).Take(10_000).Select(item => new ExportedAudit(item.Id,
                item.WorkspaceId, item.Action, item.TargetType, item.TargetId, item.Timestamp, item.Result))
            .ToListAsync(cancellationToken);
        return new UserDataExport(user.Id, user.Email ?? string.Empty, user.DisplayName, user.CreatedAt,
            user.LastLoginAt, memberships, files, shares, audits, DateTimeOffset.UtcNow);
    }
}
