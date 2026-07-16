using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VaultShare.Application.Notifications;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Notifications;

internal sealed class NotificationCenterService(VaultShareDbContext dbContext,
    UserManager<ApplicationUser> userManager) : INotificationCenterService
{
    public async Task<IReadOnlyList<NotificationSummary>> ListAsync(ClaimsPrincipal principal, int take,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
        if (take is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(take));
        return await dbContext.Notifications.AsNoTracking().Where(item => item.UserId == user.Id)
            .OrderByDescending(item => item.CreatedAt).Take(take)
            .Select(item => new NotificationSummary(item.Id, item.Type, item.Title, item.Message, item.CreatedAt, item.ReadAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkReadAsync(Guid notificationId, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal) ?? throw new UnauthorizedAccessException();
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(item => item.Id == notificationId && item.UserId == user.Id, cancellationToken);
        if (notification is null) return false;
        notification.MarkRead(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

internal sealed class NotificationDeliveryService(VaultShareDbContext dbContext, IEmailService emailService,
    ILogger<NotificationDeliveryService> logger) : INotificationDeliveryService
{
    public async Task<int> DeliverPendingEmailAsync(int batchSize, CancellationToken cancellationToken)
    {
        var notifications = await (from notification in dbContext.Notifications
                                   join user in dbContext.Users on notification.UserId equals user.Id
                                   where notification.EmailRequested && notification.EmailSentAt == null &&
                                         notification.EmailAttempts < 5 && user.Email != null && user.DeletedAt == null
                                   orderby notification.CreatedAt
                                   select new { Notification = notification, Email = user.Email })
            .Take(batchSize).ToListAsync(cancellationToken);
        foreach (var item in notifications)
        {
            try
            {
                var safeTitle = HtmlEncoder.Default.Encode(item.Notification.Title);
                var safeMessage = HtmlEncoder.Default.Encode(item.Notification.Message);
                await emailService.SendAsync(new EmailMessage(item.Email!, item.Notification.Title,
                    $"{item.Notification.Title}\n\n{item.Notification.Message}", $"<h1>{safeTitle}</h1><p>{safeMessage}</p>"), cancellationToken);
                item.Notification.MarkEmailSent(DateTimeOffset.UtcNow);
            }
            catch (Exception exception)
            {
                item.Notification.MarkEmailFailed();
                logger.LogWarning(exception, "Notification email delivery failed for {NotificationId}", item.Notification.Id);
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return notifications.Count;
    }
}
