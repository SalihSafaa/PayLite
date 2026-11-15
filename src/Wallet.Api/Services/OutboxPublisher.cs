using System.Text;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PayLite.Contracts;
using Serilog.Context;

namespace PayLite.WalletApi;

/// <summary>
/// Publishes unsent outbox rows to Kafka (ADR-0004).
/// "Delivered" for the producer = the broker acknowledged the write (Acks=All); only then is SentAt set.
/// If Kafka is down, rows simply stay unsent and we back off: transfers never wait for Kafka.
/// </summary>
public sealed class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxPublisher> _logger;
    private readonly IProducer<string, string> _producer;

    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = kafkaOptions.Value.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            // Fail a send after 10s instead of the 5-minute default, so a dead broker is noticed quickly.
            MessageTimeoutMs = 10_000
        })
            // librdkafka prints its own logs to stderr by default (no correlation id, twice a second
            // while Kafka is down). Send them through our logger at Debug; the "Could not publish"
            // warning below is the line that matters.
            .SetLogHandler((_, log) => _logger.LogDebug("librdkafka {Facility}: {Message}", log.Facility, log.Message))
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backoff = TimeSpan.FromSeconds(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            bool allSent;
            try
            {
                allSent = await PublishBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // e.g. the database is unreachable. Log and try again later; never crash the API.
                _logger.LogWarning(ex, "Outbox publishing failed.");
                allSent = false;
            }

            if (allSent)
            {
                backoff = TimeSpan.FromSeconds(1);
                await Task.Delay(_options.PollIntervalMs, stoppingToken);
            }
            else
            {
                await Task.Delay(backoff, stoppingToken);
                backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, _options.MaxBackoffSeconds));
            }
        }
    }

    /// <returns>false if a publish failed (so the caller backs off).</returns>
    private async Task<bool> PublishBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        if (!context.Database.IsRelational())
            return true;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // UPDLOCK: these rows are ours until we commit. READPAST: a second API instance skips
        // rows another instance is publishing, instead of waiting for them or sending them twice.
        var batch = await context.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT TOP ({_options.BatchSize}) *
                FROM wallet.OutboxMessages WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE SentAt IS NULL
                ORDER BY Id
                """)
            .ToListAsync(cancellationToken);

        var allSent = true;
        foreach (var message in batch)
        {
            using (LogContext.PushProperty("CorrelationId", message.CorrelationId))
            {
                try
                {
                    var result = await _producer.ProduceAsync(message.Topic, new Message<string, string>
                    {
                        Key = message.MessageKey,
                        Value = message.Payload,
                        Headers = new Headers
                        {
                            { CorrelationHeaders.CorrelationId, Encoding.UTF8.GetBytes(message.CorrelationId) },
                            { "event-type", Encoding.UTF8.GetBytes(message.EventType) }
                        }
                    }, cancellationToken);

                    if (result.Status != PersistenceStatus.Persisted)
                        throw new KafkaException(new Error(ErrorCode.Local_Fail, $"Delivery status {result.Status}"));

                    message.SentAt = DateTime.UtcNow;
                    message.Attempts++;
                    _logger.LogInformation("Published event {EventId} ({EventType}) to {Topic} [{Partition}] @ {Offset}",
                        message.EventId, message.EventType, result.Topic, result.Partition.Value, result.Offset.Value);
                }
                catch (ProduceException<string, string> ex)
                {
                    message.Attempts++;
                    message.LastError = Truncate(ex.Error.Reason);
                    _logger.LogWarning("Could not publish event {EventId}: {Reason}. Will retry.", message.EventId, ex.Error.Reason);
                    // Keep order per key: stop here and retry this row (and the rest) after the back-off.
                    allSent = false;
                    break;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return allSent;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        _producer.Flush(TimeSpan.FromSeconds(5));
    }

    public override void Dispose()
    {
        _producer.Dispose();
        base.Dispose();
    }

    private static string Truncate(string value) => value.Length <= 1000 ? value : value[..1000];
}
