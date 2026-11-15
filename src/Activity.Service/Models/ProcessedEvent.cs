namespace PayLite.ActivityService;

/// <summary>
/// One row per Kafka event that has been fully applied (ADR-0004).
/// <see cref="EventId"/> is the PRIMARY KEY, so inserting the same event twice fails with
/// SQL error 2627 inside the same transaction as its effects: the duplicate is rolled back and skipped.
/// </summary>
public class ProcessedEvent
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}
