using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PayLite.ActivityService;

/// <summary>
/// /hubs/notifications. The browser only listens: the server calls "notification" on the connections
/// of ONE user (Clients.User, keyed by the JWT's NameIdentifier, the same id as Notification.UserId).
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public const string Path = "/hubs/notifications";
    public const string NotificationMethod = "notification";
}
