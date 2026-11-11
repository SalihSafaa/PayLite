namespace PayLite.Contracts;

/// <summary>
/// The wrapper every event on Kafka is sent in. See docs/event-contract.md.
/// </summary>
/// <param name="EventId">Unique per event. The consumer uses it to detect duplicates.</param>
/// <param name="EventType">One of <see cref="EventTypes"/>.</param>
/// <param name="Version">Schema version of <typeparamref name="TData"/>, so the payload can change without breaking the consumer.</param>
/// <param name="OccurredAt">When the business fact happened, in UTC.</param>
/// <param name="CorrelationId">Carried from the original HTTP request, so one transfer can be traced through every log line.</param>
/// <param name="Data">The event payload.</param>
public sealed record EventEnvelope<TData>(
    Guid EventId,
    string EventType,
    int Version,
    DateTime OccurredAt,
    string CorrelationId,
    TData Data);
