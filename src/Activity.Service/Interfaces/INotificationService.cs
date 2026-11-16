namespace PayLite.ActivityService;

public interface INotificationService
{
    Task<NotificationListDto> GetMyNotificationsAsync(int userId, bool unreadOnly, int limit, CancellationToken cancellationToken);
    Task<Result<NotificationDto>> MarkAsReadAsync(int userId, long notificationId, CancellationToken cancellationToken);
}
