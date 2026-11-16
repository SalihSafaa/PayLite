using Microsoft.EntityFrameworkCore;

namespace PayLite.ActivityService;

public class NotificationService : INotificationService
{
    private readonly ActivityDbContext _context;

    public NotificationService(ActivityDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationListDto> GetMyNotificationsAsync(int userId, bool unreadOnly, int limit, CancellationToken cancellationToken)
    {
        var mine = _context.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        var items = await (unreadOnly ? mine.Where(n => n.ReadAt == null) : mine)
            .OrderByDescending(n => n.OccurredAt)
            .ThenByDescending(n => n.Id)
            .Take(limit)
            .Select(n => ToDto(n))
            .ToListAsync(cancellationToken);

        var unreadCount = await mine.CountAsync(n => n.ReadAt == null, cancellationToken);

        return new NotificationListDto { Items = items, UnreadCount = unreadCount };
    }

    public async Task<Result<NotificationDto>> MarkAsReadAsync(int userId, long notificationId, CancellationToken cancellationToken)
    {
        // Filtering by the owner too: someone else's notification is "not found", so ids can't be probed.
        var notification = await _context.Notifications
            .SingleOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

        if (notification is null)
            return Result<NotificationDto>.Failure(ErrorType.NotFound, "Notification not found.");

        // Idempotent: marking an already-read notification keeps the first ReadAt.
        if (notification.ReadAt is null)
        {
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result<NotificationDto>.Success(ToDto(notification));
    }

    public static NotificationDto ToDto(Notification n) => new()
    {
        Id = n.Id,
        TransferId = n.TransferId,
        FromUsername = n.FromUsername,
        Amount = n.Amount,
        Currency = n.Currency,
        OccurredAt = DateTime.SpecifyKind(n.OccurredAt, DateTimeKind.Utc),
        ReadAt = n.ReadAt is null ? null : DateTime.SpecifyKind(n.ReadAt.Value, DateTimeKind.Utc)
    };
}
