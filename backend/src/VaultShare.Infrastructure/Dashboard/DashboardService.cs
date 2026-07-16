using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VaultShare.Application.Dashboard;
using VaultShare.Domain.Files;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Dashboard;

internal sealed class DashboardService(VaultShareDbContext dbContext,
    UserManager<ApplicationUser> userManager, IConfiguration configuration) : IDashboardService
{
    public async Task<DashboardSummary?> GetAsync(Guid workspaceId, ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null || !await dbContext.WorkspaceMembers.AsNoTracking().AnyAsync(member =>
            member.WorkspaceId == workspaceId && member.UserId == user.Id && member.RemovedAt == null,
            cancellationToken)) return null;

        var now = DateTimeOffset.UtcNow;
        var since = now.Date.AddDays(-6);
        var files = dbContext.StoredFiles.AsNoTracking().Where(file => file.WorkspaceId == workspaceId && file.PurgedAt == null);
        var shares = dbContext.Shares.AsNoTracking().Where(share => share.WorkspaceId == workspaceId);
        var uploadDates = await files.Where(file => file.CreatedAt >= since)
            .Select(file => file.CreatedAt).ToListAsync(cancellationToken);
        var downloadDates = await (from download in dbContext.FileDownloads.AsNoTracking()
                                   join share in shares on download.ShareId equals share.Id
                                   where download.StartedAt >= since
                                   select download.StartedAt).ToListAsync(cancellationToken);
        var activity = Enumerable.Range(0, 7).Select(offset => DateOnly.FromDateTime(since.AddDays(offset))).Select(date =>
            new DailyActivity(date, uploadDates.Count(item => DateOnly.FromDateTime(item.Date) == date),
                downloadDates.Count(item => DateOnly.FromDateTime(item.Date) == date))).ToList();
        var quota = await dbContext.WorkspaceSettings.AsNoTracking().Where(setting => setting.WorkspaceId == workspaceId)
            .Select(setting => (long?)setting.StorageQuotaBytes).SingleOrDefaultAsync(cancellationToken)
            ?? (long.TryParse(configuration["WORKSPACE_DEFAULT_QUOTA"], out var configuredQuota) && configuredQuota > 0
                ? configuredQuota : 10L * 1024 * 1024 * 1024);

        return new DashboardSummary(
            await files.Where(file => file.DeletedAt == null).SumAsync(file => file.FileSize, cancellationToken),
            quota,
            await files.LongCountAsync(cancellationToken),
            await shares.LongCountAsync(share => !share.IsRevoked && share.ExpiresAt > now, cancellationToken),
            await shares.LongCountAsync(share => share.ExpiresAt <= now, cancellationToken),
            downloadDates.Count,
            uploadDates.Count,
            await files.LongCountAsync(file => file.AvailabilityStatus == AvailabilityStatus.Processing, cancellationToken),
            await files.LongCountAsync(file => file.AvailabilityStatus == AvailabilityStatus.Quarantined, cancellationToken),
            await shares.LongCountAsync(share => !share.IsRevoked && share.ExpiresAt > now && share.ExpiresAt <= now.AddDays(7), cancellationToken),
            activity);
    }
}
