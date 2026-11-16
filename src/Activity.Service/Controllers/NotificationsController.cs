using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PayLite.ActivityService;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private const int MaxLimit = 100;
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    /// <summary>My notifications, newest first, plus my total unread count.</summary>
    [HttpGet]
    public async Task<ActionResult<NotificationListDto>> GetMine(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = $"limit must be between 1 and {MaxLimit}."
            });
        }

        return Ok(await _notifications.GetMyNotificationsAsync(User.GetUserId(), unreadOnly, limit, cancellationToken));
    }

    /// <summary>Marks one of my notifications as read. Someone else's notification is 404.</summary>
    [HttpPost("{id:long}/read")]
    public async Task<ActionResult<NotificationDto>> MarkAsRead(long id, CancellationToken cancellationToken)
    {
        var result = await _notifications.MarkAsReadAsync(User.GetUserId(), id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToActionResult(result);
    }
}
