namespace PayLite.ActivityService;

/// <summary>Pushes a new notification to the browser of the user it belongs to (SignalR).</summary>
public interface INotificationPusher
{
    /// <summary>Best effort: never throws. A missed push still shows up in GET /api/notifications.</summary>
    Task PushAsync(int userId, NotificationDto notification, CancellationToken cancellationToken);
}
