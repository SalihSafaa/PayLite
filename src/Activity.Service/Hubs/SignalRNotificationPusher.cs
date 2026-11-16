using System.Globalization;
using Microsoft.AspNetCore.SignalR;

namespace PayLite.ActivityService;

public class SignalRNotificationPusher : INotificationPusher
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<SignalRNotificationPusher> _logger;

    public SignalRNotificationPusher(IHubContext<NotificationHub> hub, ILogger<SignalRNotificationPusher> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task PushAsync(int userId, NotificationDto notification, CancellationToken cancellationToken)
    {
        try
        {
            await _hub.Clients.User(userId.ToString(CultureInfo.InvariantCulture))
                .SendAsync(NotificationHub.NotificationMethod, notification, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not part of "delivered" (ADR-0004): the row is committed, the list endpoint still shows it.
            _logger.LogWarning(ex, "Could not push notification {NotificationId} to user {UserId}", notification.Id, userId);
        }
    }
}
