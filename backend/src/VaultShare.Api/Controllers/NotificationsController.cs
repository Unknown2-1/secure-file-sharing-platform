using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VaultShare.Application.Notifications;

namespace VaultShare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public sealed class NotificationsController(INotificationCenterService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationSummary>>> List(int take = 50,
        CancellationToken cancellationToken = default) => take is < 1 or > 100
        ? BadRequest()
        : Ok(await notifications.ListAsync(User, take, cancellationToken));

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken) =>
        await notifications.MarkReadAsync(notificationId, User, cancellationToken) ? NoContent() : NotFound();
}
