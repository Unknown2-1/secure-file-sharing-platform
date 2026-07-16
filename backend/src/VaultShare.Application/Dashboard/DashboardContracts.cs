using System.Security.Claims;

namespace VaultShare.Application.Dashboard;

public sealed record DailyActivity(DateOnly Date, int Uploads, int Downloads);
public sealed record DashboardSummary(long StorageBytes, long StorageQuotaBytes, long TotalFiles,
    long ActiveShares, long ExpiredShares, long DownloadsLastSevenDays, long UploadsLastSevenDays,
    long ProcessingFiles, long QuarantinedFiles, long SharesExpiringSoon,
    IReadOnlyList<DailyActivity> Activity);

public interface IDashboardService
{
    Task<DashboardSummary?> GetAsync(Guid workspaceId, ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}
