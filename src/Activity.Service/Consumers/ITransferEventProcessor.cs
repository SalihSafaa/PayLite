namespace PayLite.ActivityService;

/// <summary>
/// Applies one Kafka message to ActivityDb. The consumer depends on this interface so tests can
/// swap in a processor that fails on purpose (transient errors, retries, dead-letter).
/// </summary>
public interface ITransferEventProcessor
{
    /// <exception cref="PoisonMessageException">The message can never be processed.</exception>
    Task<ProcessOutcome> ProcessAsync(string? json, CancellationToken cancellationToken);
}
