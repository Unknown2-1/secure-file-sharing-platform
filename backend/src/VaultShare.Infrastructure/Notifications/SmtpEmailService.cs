using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using VaultShare.Application.Notifications;

namespace VaultShare.Infrastructure.Notifications;

internal sealed class SmtpEmailService(IConfiguration configuration) : IEmailService
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var host = configuration["SMTP_HOST"];
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("SMTP_HOST is required.");
        }

        var port = int.TryParse(configuration["SMTP_PORT"], out var configuredPort) ? configuredPort : 25;
        var from = configuration["EMAIL_FROM"] ?? throw new InvalidOperationException("EMAIL_FROM is required.");
        using var mail = new MailMessage
        {
            From = new MailAddress(from),
            Subject = message.Subject,
            Body = message.TextBody,
        };
        mail.To.Add(new MailAddress(message.Recipient));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.TextBody, null, "text/plain"));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, null, "text/html"));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = bool.TryParse(configuration["SMTP_USE_SSL"], out var useSsl) && useSsl,
        };
        var username = configuration["SMTP_USERNAME"];
        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, configuration["SMTP_PASSWORD"]);
        }

        await client.SendMailAsync(mail, cancellationToken);
    }
}
