namespace VaultShare.Domain.Shares;

public sealed class FileDownload
{
    private FileDownload() { }
    public FileDownload(Guid id, Guid shareId, Guid storedFileId, Guid sessionId, DateTimeOffset startedAt)
    { Id = id; ShareId = shareId; StoredFileId = storedFileId; SessionId = sessionId; StartedAt = startedAt; }
    public Guid Id { get; private set; }
    public Guid ShareId { get; private set; }
    public Guid StoredFileId { get; private set; }
    public Guid SessionId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public void Complete(DateTimeOffset now) => CompletedAt ??= now;
    public void Fail(DateTimeOffset now) => FailedAt ??= now;
}
