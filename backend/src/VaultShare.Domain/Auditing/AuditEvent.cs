namespace VaultShare.Domain.Auditing;

public sealed class AuditEvent
{
    private AuditEvent() { }
    public AuditEvent(Guid id, Guid? workspaceId, Guid? actorUserId, string action, string targetType,
        string? targetId, DateTimeOffset timestamp, string ipAddressHash, string userAgent,
        string correlationId, string result, string safeMetadataJson)
    {
        Id = id; WorkspaceId = workspaceId; ActorUserId = actorUserId; Action = action;
        TargetType = targetType; TargetId = targetId; Timestamp = timestamp;
        IpAddressHash = ipAddressHash; UserAgent = userAgent; CorrelationId = correlationId;
        Result = result; SafeMetadataJson = safeMetadataJson;
    }
    public Guid Id { get; private set; }
    public Guid? WorkspaceId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public string? TargetId { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string IpAddressHash { get; private set; } = string.Empty;
    public string UserAgent { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public string Result { get; private set; } = string.Empty;
    public string SafeMetadataJson { get; private set; } = "{}";
}
