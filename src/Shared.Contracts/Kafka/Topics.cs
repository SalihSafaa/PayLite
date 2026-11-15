namespace PayLite.Contracts;

/// <summary>
/// Kafka topic names.
/// </summary>
/// <remarks>
/// Decided in docs/adr/0004-kafka-topic-and-delivery.md. The message key is <c>toWalletId</c>
/// (as a string), so all events for one recipient stay in order on one partition.
/// The topics are created by the <c>kafka-init</c> service in docker-compose.yml, so keep them in sync.
/// </remarks>
public static class Topics
{
    public const string Transfers = "paylite.transfers";
    public const string TransfersDeadLetter = "paylite.transfers.dlt";
}
