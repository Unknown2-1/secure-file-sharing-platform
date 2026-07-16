using VaultShare.Application.Maintenance;
using VaultShare.Application.Uploads;

namespace VaultShare.Worker;

public sealed class Worker(IServiceScopeFactory scopeFactory, IConfiguration configuration,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<ICompletedUploadProcessor>();
                var processed = await processor.ProcessBatchAsync(10, stoppingToken);
                if (processed > 0) logger.LogInformation("Processed {UploadCount} completed uploads", processed);
                var cleanup = scope.ServiceProvider.GetRequiredService<IUploadMaintenanceService>();
                var cleaned = await cleanup.CleanupAbandonedAsync(100, stoppingToken);
                if (cleaned > 0) logger.LogInformation("Cleaned {UploadCount} abandoned upload records", cleaned);
                var notifications = scope.ServiceProvider.GetRequiredService<VaultShare.Application.Notifications.INotificationDeliveryService>();
                var delivered = await notifications.DeliverPendingEmailAsync(50, stoppingToken);
                if (delivered > 0) logger.LogInformation("Processed {NotificationCount} notification emails", delivered);
                var maintenance = await scope.ServiceProvider.GetRequiredService<IMaintenanceService>()
                    .RunAsync(100, stoppingToken);
                if (maintenance != new MaintenanceResult(0, 0, 0, 0, 0, 0, 0))
                    logger.LogInformation("Maintenance completed: {@MaintenanceResult}", maintenance);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Upload cleanup iteration failed");
            }

            var interval = TimeSpan.TryParse(configuration["WORKER_POLL_INTERVAL"], out var configured) && configured > TimeSpan.Zero
                ? configured : TimeSpan.FromSeconds(10);
            await Task.Delay(interval, stoppingToken);
        }
    }
}
