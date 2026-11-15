namespace PayLite.WalletApi;

/// <summary>
/// An event waiting to be published to Kafka (transactional outbox, ADR-0004).
/// The row is inserted by wallet.usp_Transfer in the SAME transaction as the money movement,
/// so an event exists if and only if the transfer committed.
/// </summary>
public class OutboxMessage
{
    public long Id { get; set; }
    /// <summary>The envelope's eventId: the same value on every retry, so the consumer can dedupe.</summary>
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    /// <summary>Kafka message key: toWalletId as a string (ADR-0004).</summary>
    public string MessageKey { get; set; } = string.Empty;
    /// <summary>The complete JSON envelope, exactly as it will be sent.</summary>
    public string Payload { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    /// <summary>Set only after the broker acknowledged the write. NULL = still to publish.</summary>
    public DateTime? SentAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
