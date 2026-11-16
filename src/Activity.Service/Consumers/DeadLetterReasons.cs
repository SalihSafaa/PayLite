namespace PayLite.ActivityService;

/// <summary>Values of the <c>dlt-reason</c> header on <c>paylite.transfers.dlt</c> (ADR-0004).</summary>
public static class DeadLetterReasons
{
    /// <summary>The message can never be processed (invalid JSON or UTF-8, missing fields, unknown version...). Not retried.</summary>
    public const string Poison = "poison";

    /// <summary>A transient error (e.g. SQL Server down) that didn't go away after all retries.</summary>
    public const string RetriesExhausted = "retries-exhausted";
}

/// <summary>Header names added to every dead-lettered message, next to the original headers.</summary>
public static class DeadLetterHeaders
{
    public const string Reason = "dlt-reason";
    public const string Error = "dlt-error";
    public const string OriginalTopic = "dlt-original-topic";
    public const string OriginalPartition = "dlt-original-partition";
    public const string OriginalOffset = "dlt-original-offset";
    public const string FailedAt = "dlt-failed-at";
    public const string Attempts = "dlt-attempts";
}
