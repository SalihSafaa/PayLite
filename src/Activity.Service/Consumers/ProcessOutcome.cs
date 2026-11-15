namespace PayLite.ActivityService;

public enum ProcessOutcome
{
    /// <summary>
    /// The effects were written and committed.
    /// </summary>
    Processed,

    /// <summary>
    /// This eventId was already processed (SQL error 2627): nothing was written again.
    /// </summary>
    Duplicate,

    /// <summary>
    /// An event type this service doesn't consume: ignored on purpose (forward compatible).
    /// </summary>
    IgnoredEventType
}
