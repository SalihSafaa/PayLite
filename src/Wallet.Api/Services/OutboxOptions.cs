namespace PayLite.WalletApi;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>How often to look for unsent rows when everything is healthy.</summary>
    public int PollIntervalMs { get; set; } = 1000;

    /// <summary>Rows published per batch.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Upper bound for the back-off while Kafka is unreachable (1s, 2s, 4s ... up to this).</summary>
    public int MaxBackoffSeconds { get; set; } = 30;
}
