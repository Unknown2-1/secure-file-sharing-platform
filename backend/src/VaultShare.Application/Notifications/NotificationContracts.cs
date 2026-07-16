using System.Security.Claims;

namespace VaultShare.Application.Notifications;

public sealed record NotificationSummary(Guid Id, string Type, string Title, string Message,
    DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
public interface INotificationCenterService
{
    Task<IReadOnlyList<NotificationSummary>> ListAsync(ClaimsPrincipal principal, int take,
        CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(Guid notificationId, ClaimsPrincipal principal, CancellationToken cancellationToken);
}
public interface INotificationDeliveryService
{
    Task<int> DeliverPendingEmailAsync(int batchSize, CancellationToken cancellationToken);
}
