using Microsoft.EntityFrameworkCore;
using VaultShare.Application.Auditing;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Auditing;

internal sealed class AuditQueryService(VaultShareDbContext dbContext) : IAuditQueryService
{
    public async Task<AuditPage> ListAsync(Guid workspaceId, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(page));
        var query = dbContext.AuditEvents.AsNoTracking().Where(audit => audit.WorkspaceId == workspaceId);
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderByDescending(audit => audit.Timestamp).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(audit => new AuditEventSummary(audit.Id, audit.WorkspaceId, audit.ActorUserId, audit.Action,
                audit.TargetType, audit.TargetId, audit.Timestamp, audit.CorrelationId, audit.Result, audit.SafeMetadataJson))
            .ToListAsync(cancellationToken);
        return new(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<AuditEventSummary>> ExportAsync(Guid workspaceId, DateTimeOffset? from,
        DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var query = dbContext.AuditEvents.AsNoTracking().Where(audit => audit.WorkspaceId == workspaceId);
        if (from is not null) query = query.Where(audit => audit.Timestamp >= from);
        if (to is not null) query = query.Where(audit => audit.Timestamp < to);
        return await query.OrderBy(audit => audit.Timestamp).Take(100_000)
            .Select(audit => new AuditEventSummary(audit.Id, audit.WorkspaceId, audit.ActorUserId, audit.Action,
                audit.TargetType, audit.TargetId, audit.Timestamp, audit.CorrelationId, audit.Result, audit.SafeMetadataJson))
            .ToListAsync(cancellationToken);
    }
}
