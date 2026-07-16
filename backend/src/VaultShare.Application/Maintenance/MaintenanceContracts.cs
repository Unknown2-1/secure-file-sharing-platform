namespace VaultShare.Application.Maintenance;

public sealed record MaintenanceResult(
    int ExpiredSharesNotified,
    int PurgedFiles,
    int ExpiredSessionsDeleted,
    int AccessAttemptsDeleted,
    int NotificationsDeleted,
    int AuditEventsDeleted,
    int StaleUploadsFailed);

public interface IMaintenanceService
{
    Task<MaintenanceResult> RunAsync(int batchSize, CancellationToken cancellationToken);
}

public sealed record StorageConsistencyResult(int DatabaseFilesChecked, IReadOnlyList<Guid> MissingFileIds,
    int ObjectsScanned, int OrphanObjects, int OrphanObjectsDeleted, bool DeleteMode);

public interface IStorageConsistencyService
{
    Task<StorageConsistencyResult> CheckMissingObjectsAsync(int batchSize, CancellationToken cancellationToken);
    Task<StorageConsistencyResult> ReconcileAsync(int batchSize, bool deleteOrphans,
        CancellationToken cancellationToken);
}
