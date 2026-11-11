namespace PayLite.Contracts;

/// <summary>
/// Header names shared by the frontend, both services and the Kafka messages.
/// </summary>
public static class CorrelationHeaders
{
    /// <summary>HTTP request/response header and Kafka message header carrying the correlation id.</summary>
    public const string CorrelationId = "X-Correlation-ID";

    /// <summary>Header the frontend sends with every transfer (see ADR-0002).</summary>
    public const string IdempotencyKey = "Idempotency-Key";
}
