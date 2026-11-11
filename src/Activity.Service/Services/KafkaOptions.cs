namespace PayLite.ActivityService;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>e.g. "localhost:29092" from the host, "kafka:9092" inside compose.</summary>
    public string BootstrapServers { get; set; } = string.Empty;
}
