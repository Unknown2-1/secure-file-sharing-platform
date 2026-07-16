namespace VaultShare.Domain.Shares;

public sealed class ShareAccessAttempt
{
    private ShareAccessAttempt() { }
    public ShareAccessAttempt(Guid id, Guid shareId, bool succeeded, string resultCode,
        string ipAddressHash, string userAgent, DateTimeOffset attemptedAt)
    {
        Id = id; ShareId = shareId; Succeeded = succeeded; ResultCode = resultCode;
        IpAddressHash = ipAddressHash; UserAgent = userAgent; AttemptedAt = attemptedAt;
    }
    public Guid Id { get; private set; }
    public Guid ShareId { get; private set; }
    public bool Succeeded { get; private set; }
    public string ResultCode { get; private set; } = string.Empty;
    public string IpAddressHash { get; private set; } = string.Empty;
    public string UserAgent { get; private set; } = string.Empty;
    public DateTimeOffset AttemptedAt { get; private set; }
}
