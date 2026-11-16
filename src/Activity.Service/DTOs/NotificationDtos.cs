namespace PayLite.ActivityService;

/// <summary>One "you received money" notification. Also the payload pushed over SignalR.</summary>
public class NotificationDto
{
    public long Id { get; set; }
    public long TransferId { get; set; }
    public string FromUsername { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    /// <summary>When the transfer happened (UTC).</summary>
    public DateTime OccurredAt { get; set; }
    /// <summary>null = unread.</summary>
    public DateTime? ReadAt { get; set; }
}

public class NotificationListDto
{
    /// <summary>Newest first (by when the transfer happened).</summary>
    public List<NotificationDto> Items { get; set; } = [];
    /// <summary>All unread notifications of the user, not only the ones in <see cref="Items"/>. Drives the bell.</summary>
    public int UnreadCount { get; set; }
}
