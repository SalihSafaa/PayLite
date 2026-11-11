using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace PayLite.WalletApi;

/// <summary>
/// Healthy when the broker answers a metadata request.
/// </summary>
/// <remarks>
/// Kafka being down must NOT stop transfers (the outbox absorbs it), so this check is
/// registered as <see cref="HealthStatus.Degraded"/>, not Unhealthy: /health still tells you
/// Kafka is gone, but an orchestrator won't kill a perfectly working API because of it.
/// </remarks>
public class KafkaHealthCheck : IHealthCheck
{
    private readonly KafkaOptions _options;

    public KafkaHealthCheck(IOptions<KafkaOptions> options)
    {
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var admin = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = _options.BootstrapServers,
                SocketTimeoutMs = 3000
            }).Build();

            var metadata = admin.GetMetadata(TimeSpan.FromSeconds(3));
            return Task.FromResult(metadata.Brokers.Count > 0
                ? HealthCheckResult.Healthy($"{metadata.Brokers.Count} broker(s) reachable.")
                : new HealthCheckResult(context.Registration.FailureStatus, "No Kafka brokers in metadata."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, "Kafka is unreachable.", ex));
        }
    }
}
