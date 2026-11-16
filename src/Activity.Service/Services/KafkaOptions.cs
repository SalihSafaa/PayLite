namespace PayLite.ActivityService;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>e.g. "localhost:29092" from the host, "kafka:9092" inside compose.</summary>
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>The consumer group (ADR-0004). Every instance of this service shares it.</summary>
    public string ConsumerGroupId { get; set; } = "activity-service";

    /// <summary>Set to false to run the API without the background consumer.</summary>
    public bool ConsumerEnabled { get; set; } = true;

    /// <summary>Retries for a transient failure before the message goes to the dead-letter topic (ADR-0004: 3).</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>First retry delay; it doubles on every retry (1s, 2s, 4s). Tests set it lower.</summary>
    public int RetryBaseDelayMs { get; set; } = 1000;
}
