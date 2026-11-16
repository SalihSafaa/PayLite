using System.Globalization;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using PayLite.Contracts;

namespace PayLite.ActivityService;

/// <summary>
/// Reads paylite.transfers in the "activity-service" consumer group and hands each message to
/// <see cref="ITransferEventProcessor"/> (ADR-0004).
/// </summary>
/// <remarks>
/// The offset is committed ONLY when the message is finished with: its effects are committed, it was a
/// duplicate, or it is safely on the dead-letter topic. A crash before that just redelivers the message.
/// <list type="bullet">
/// <item>Poison (invalid UTF-8 or JSON, missing fields, unknown version): straight to the DLT, no retries.</item>
/// <item>Anything else (SQL Server down, timeout, deadlock): retried <see cref="KafkaOptions.MaxRetries"/> times
/// (1s, 2s, 4s), then to the DLT. The partition is blocked for a few seconds at most, never forever.</item>
/// <item>If the DLT write itself fails, nothing is committed: the same message is tried again a moment later.</item>
/// </list>
/// Keys and values are read as raw bytes, so even a value that isn't text reaches our code (and the DLT)
/// instead of failing inside Kafka's deserializer and blocking the partition.
/// </remarks>
public class TransferEventsConsumer : BackgroundService
{
    private static readonly TimeSpan PauseAfterFailure = TimeSpan.FromSeconds(2);
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<TransferEventsConsumer> _logger;

    public TransferEventsConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<TransferEventsConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    // Consume() blocks, so the loop runs on its own thread instead of holding up the host's startup.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(
            () => RunAsync(stoppingToken),
            stoppingToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            EnableAutoCommit = false,                  // we commit by hand, when a message is finished with
            AutoOffsetReset = AutoOffsetReset.Earliest // a new group (or a rebuild) starts at the beginning
        };

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 10_000
        };

        using var consumer = new ConsumerBuilder<byte[]?, byte[]?>(consumerConfig)
            .SetLogHandler((_, log) => _logger.LogDebug("librdkafka {Facility}: {Message}", log.Facility, log.Message))
            .SetErrorHandler((_, error) => LogKafkaError(error))
            .Build();

        using var deadLetters = new ProducerBuilder<byte[]?, byte[]?>(producerConfig)
            .SetLogHandler((_, log) => _logger.LogDebug("librdkafka {Facility}: {Message}", log.Facility, log.Message))
            .SetErrorHandler((_, error) => LogKafkaError(error))
            .Build();

        consumer.Subscribe(Topics.Transfers);
        _logger.LogInformation("Consuming {Topic} as group {GroupId}", Topics.Transfers, consumerConfig.GroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<byte[]?, byte[]?> result;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    // e.g. the topic doesn't exist yet, or the broker is briefly unreachable.
                    _logger.LogWarning("Kafka consume error: {Reason}", ex.Error.Reason);
                    await Task.Delay(PauseAfterFailure, stoppingToken);
                    continue;
                }

                if (result?.Message is null)
                    continue;

                if (await HandleAsync(result, deadLetters, stoppingToken))
                {
                    consumer.Commit(result);
                }
                else
                {
                    // Not finished with (the DLT write failed): go back to this offset and try again.
                    consumer.Seek(result.TopicPartitionOffset);
                    await Task.Delay(PauseAfterFailure, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            consumer.Close(); // leave the group cleanly so the partitions move to another instance at once
            deadLetters.Flush(TimeSpan.FromSeconds(5));
        }
    }

    /// <returns>true when the offset may be committed.</returns>
    private async Task<bool> HandleAsync(
        ConsumeResult<byte[]?, byte[]?> result,
        IProducer<byte[]?, byte[]?> deadLetters,
        CancellationToken stoppingToken)
    {
        // The header is there even when the value can't be parsed, so poison messages are traceable too.
        var correlationId = ReadHeader(result.Message.Headers, CorrelationHeaders.CorrelationId) ?? "-";
        using var logScope = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });

        string? json;
        try
        {
            json = result.Message.Value is null ? null : StrictUtf8.GetString(result.Message.Value);
        }
        catch (DecoderFallbackException ex)
        {
            return await DeadLetterAsync(result, deadLetters, DeadLetterReasons.Poison, "The value is not valid UTF-8.", 1, ex, stoppingToken);
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                // A new scope (and DbContext) per attempt: a failed SaveChanges leaves entities tracked.
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<ITransferEventProcessor>();
                await processor.ProcessAsync(json, stoppingToken);
                return true;
            }
            catch (PoisonMessageException ex)
            {
                return await DeadLetterAsync(result, deadLetters, DeadLetterReasons.Poison, ex.Message, attempt, ex, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                if (attempt > _options.MaxRetries)
                {
                    return await DeadLetterAsync(result, deadLetters, DeadLetterReasons.RetriesExhausted, ex.Message, attempt, ex, stoppingToken);
                }

                var delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMs * Math.Pow(2, attempt - 1));
                _logger.LogWarning(ex,
                    "Attempt {Attempt} failed for {Topic} partition {Partition} offset {Offset}; retrying in {DelayMs} ms",
                    attempt, result.Topic, result.Partition.Value, result.Offset.Value, (int)delay.TotalMilliseconds);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Copies the original key, value and headers to paylite.transfers.dlt and adds the dlt-* headers.
    /// </summary>
    /// <returns>true once the broker has acknowledged the write; false if it failed (don't commit).</returns>
    private async Task<bool> DeadLetterAsync(
        ConsumeResult<byte[]?, byte[]?> result,
        IProducer<byte[]?, byte[]?> deadLetters,
        string reason,
        string error,
        int attempts,
        Exception exception,
        CancellationToken stoppingToken)
    {
        var headers = new Headers();
        foreach (var header in result.Message.Headers ?? [])
            headers.Add(header.Key, header.GetValueBytes());

        headers.Add(DeadLetterHeaders.Reason, Encoding.UTF8.GetBytes(reason));
        headers.Add(DeadLetterHeaders.Error, Encoding.UTF8.GetBytes(Truncate(error, 1000)));
        headers.Add(DeadLetterHeaders.OriginalTopic, Encoding.UTF8.GetBytes(result.Topic));
        headers.Add(DeadLetterHeaders.OriginalPartition, Encoding.UTF8.GetBytes(result.Partition.Value.ToString(CultureInfo.InvariantCulture)));
        headers.Add(DeadLetterHeaders.OriginalOffset, Encoding.UTF8.GetBytes(result.Offset.Value.ToString(CultureInfo.InvariantCulture)));
        headers.Add(DeadLetterHeaders.FailedAt, Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        headers.Add(DeadLetterHeaders.Attempts, Encoding.UTF8.GetBytes(attempts.ToString(CultureInfo.InvariantCulture)));

        try
        {
            var delivery = await deadLetters.ProduceAsync(Topics.TransfersDeadLetter, new Message<byte[]?, byte[]?>
            {
                Key = result.Message.Key,
                Value = result.Message.Value,
                Headers = headers
            }, stoppingToken);

            _logger.LogError(exception,
                "Sent {Topic} partition {Partition} offset {Offset} to {DeadLetterTopic} ({Reason}): {Error}",
                result.Topic, result.Partition.Value, result.Offset.Value, delivery.Topic, reason, error);
            return true;
        }
        catch (ProduceException<byte[]?, byte[]?> ex)
        {
            _logger.LogError(ex,
                "Could not write {Topic} partition {Partition} offset {Offset} to the dead-letter topic: {Reason}. Not committing; will retry",
                result.Topic, result.Partition.Value, result.Offset.Value, ex.Error.Reason);
            return false;
        }
    }

    // librdkafka reports every failed reconnect while Kafka is down; keep that out of the console.
    private void LogKafkaError(Error error)
    {
        if (error.IsFatal)
            _logger.LogError("Kafka fatal error: {Reason}", error.Reason);
        else
            _logger.LogDebug("Kafka error: {Reason}", error.Reason);
    }

    private static string? ReadHeader(Headers? headers, string name) =>
        headers is not null && headers.TryGetLastBytes(name, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
