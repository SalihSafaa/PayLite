namespace PayLite.Contracts;

/// <summary>
/// Kafka topic names.
/// </summary>
/// <remarks>
/// TODO (Day 2, ADR-0004): these names are a proposal. Confirm them, and the partition key
/// (hint from the brief: what key keeps all events for one wallet in order?), in the ADR.
/// The topics are created by the <c>kafka-init</c> service in docker-compose.yml, so keep them in sync.
/// </remarks>
public static class Topics
{
    public const string Transfers = "paylite.transfers";
    public const string TransfersDeadLetter = "paylite.transfers.dlt";
}
