using VaultShare.Application.Notifications;

namespace VaultShare.Infrastructure.Notifications;

internal sealed class NullEmailService : IEmailService
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}
