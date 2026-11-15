using Confluent.Kafka;
using Microsoft.Extensions.Options;
using PayLite.Contracts;

namespace PayLite.ActivityService;

/// <summary>
/// Consumes transfer events and commits offsets only after processing has completed.
/// </summary>
public class TransferEventsConsumer : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

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

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(
            () => RunAsync(stoppingToken),
            stoppingToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string?, string?>(config).Build();
        consumer.Subscribe(Topics.Transfers);
        _logger.LogInformation("Consuming {Topic} as group {GroupId}", Topics.Transfers, config.GroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string?, string?> result;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning("Kafka consume error: {Reason}", ex.Error.Reason);
                    await Task.Delay(RetryDelay, stoppingToken);
                    continue;
                }

                if (await HandleAsync(result, stoppingToken))
                {
                    consumer.Commit(result);
                }
                else
                {
                    consumer.Seek(result.TopicPartitionOffset);
                    await Task.Delay(RetryDelay, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task<bool> HandleAsync(
        ConsumeResult<string?, string?> result,
        CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<TransferEventProcessor>();
            await processor.ProcessAsync(result.Message.Value, stoppingToken);
            return true;
        }
        catch (PoisonMessageException ex)
        {
            _logger.LogError(
                ex,
                "Poison message at {TopicPartitionOffset}, skipping it: {Reason}",
                result.TopicPartitionOffset,
                ex.Message);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Failed to process the message at {TopicPartitionOffset}, retrying",
                result.TopicPartitionOffset);
            return false;
        }
    }
}
