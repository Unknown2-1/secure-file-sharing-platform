namespace VaultShare.Application.Auditing;

public sealed record AuditEventSummary(Guid Id, Guid? WorkspaceId, Guid? ActorUserId, string Action,
    string TargetType, string? TargetId, DateTimeOffset Timestamp, string CorrelationId, string Result,
    string SafeMetadataJson);
public sealed record AuditPage(IReadOnlyList<AuditEventSummary> Items, int Page, int PageSize, long Total);

public interface IAuditQueryService
{
    Task<AuditPage> ListAsync(Guid workspaceId, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditEventSummary>> ExportAsync(Guid workspaceId, DateTimeOffset? from,
        DateTimeOffset? to, CancellationToken cancellationToken);
}
