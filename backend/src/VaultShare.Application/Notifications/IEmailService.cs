namespace VaultShare.Application.Notifications;

public sealed record EmailMessage(string Recipient, string Subject, string TextBody, string HtmlBody);

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
