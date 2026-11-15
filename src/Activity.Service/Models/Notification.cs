namespace PayLite.ActivityService;

/// <summary>
/// "You received money" for one user. Built only from a TransferCompleted event.
/// </summary>
public class Notification
{
    public long Id { get; set; }

    /// <summary>
    /// The recipient's user id: the same value as the JWT's NameIdentifier claim.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// The event that created this notification. Unique: a second guard against duplicates.
    /// </summary>
    public Guid EventId { get; set; }

    public long TransferId { get; set; }
    public string FromUsername { get; set; } = string.Empty;

    /// <summary>
    /// decimal(18,2), exactly as it arrived in the event (ADR-0001).
    /// </summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// When the transfer happened (the event's occurredAt, UTC). The list is sorted by this.
    /// </summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// When this service stored it (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// null = unread.
    /// </summary>
    public DateTime? ReadAt { get; set; }
}
