namespace VaultShare.Domain.Notifications;

public sealed class Notification
{
    private Notification() { }
    public Notification(Guid id, Guid userId, string type, string title, string message,
        bool emailRequested, DateTimeOffset createdAt)
    { Id = id; UserId = userId; Type = type; Title = title; Message = message; EmailRequested = emailRequested; CreatedAt = createdAt; }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public bool EmailRequested { get; private set; }
    public int EmailAttempts { get; private set; }
    public DateTimeOffset? EmailSentAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;
    public void MarkEmailSent(DateTimeOffset now) { EmailSentAt ??= now; EmailAttempts++; }
    public void MarkEmailFailed() => EmailAttempts++;
}
