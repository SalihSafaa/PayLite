using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayLite.Contracts;

namespace PayLite.ActivityService.Tests;

/// <summary>Shared Kafka plumbing for the consumer tests: topics, producing events, reading the DLT.</summary>
public static class KafkaTestHelper
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    // 09:15 UTC = 12:15 in Baghdad, so the summary row is (2026-11-12, hour 12).
    public static readonly DateTime OccurredAt = new(2026, 11, 12, 9, 15, 0, DateTimeKind.Utc);

    /// <summary>Creates paylite.transfers and paylite.transfers.dlt (3 partitions each), like kafka-init does.</summary>
    public static async Task EnsureTopicsAsync(string bootstrapServers)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();
        try
        {
            await admin.CreateTopicsAsync(
            [
                new TopicSpecification { Name = Topics.Transfers, NumPartitions = 3, ReplicationFactor = 1 },
                new TopicSpecification { Name = Topics.TransfersDeadLetter, NumPartitions = 3, ReplicationFactor = 1 }
            ]);
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code is ErrorCode.TopicAlreadyExists or ErrorCode.NoError))
        {
        }
    }

    public static IProducer<byte[], byte[]> CreateProducer(string bootstrapServers) =>
        new ProducerBuilder<byte[], byte[]>(new ProducerConfig { BootstrapServers = bootstrapServers }).Build();

    /// <summary>A valid TransferCompleted v1 envelope, as the Wallet API's outbox writes it.</summary>
    public static string TransferCompletedJson(Guid eventId, int toUserId, bool toIsMerchant, decimal amount, string correlationId)
    {
        var data = new TransferCompletedData(
            TransferId: 1042, FromWalletId: 11, FromUserId: 5, FromUsername: "ali",
            ToWalletId: toUserId, ToUserId: toUserId, ToIsMerchant: toIsMerchant,
            Amount: amount, Currency: "IQD");
        return JsonSerializer.Serialize(new EventEnvelope<TransferCompletedData>(
            eventId, EventTypes.TransferCompleted, TransferCompletedData.CurrentVersion,
            OccurredAt, correlationId, data), EventJson.Options);
    }

    /// <summary>Produces to paylite.transfers with key = toWalletId (ADR-0004) and the X-Correlation-ID header.</summary>
    public static Task ProduceAsync(IProducer<byte[], byte[]> producer, string key, byte[] value, string correlationId) =>
        producer.ProduceAsync(Topics.Transfers, new Message<byte[], byte[]>
        {
            Key = Encoding.UTF8.GetBytes(key),
            Value = value,
            Headers = new Headers { { CorrelationHeaders.CorrelationId, Encoding.UTF8.GetBytes(correlationId) } }
        });

    public static async Task WaitUntilProcessedAsync(IServiceProvider services, Guid eventId)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();
            if (await context.ProcessedEvents.AnyAsync(e => e.EventId == eventId))
                return;

            await Task.Delay(250);
        }

        throw new TimeoutException($"Event {eventId} was not processed within {Timeout.TotalSeconds}s.");
    }

    /// <summary>Reads paylite.transfers.dlt from the beginning until a message with this correlation id shows up.</summary>
    public static ConsumeResult<byte[], byte[]> ReadDeadLetter(string bootstrapServers, string correlationId)
    {
        using var consumer = new ConsumerBuilder<byte[], byte[]>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = $"dlt-reader-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();
        consumer.Subscribe(Topics.TransfersDeadLetter);

        try
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (DateTime.UtcNow < deadline)
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result?.Message is not null && Header(result, CorrelationHeaders.CorrelationId) == correlationId)
                    return result;
            }
        }
        finally
        {
            consumer.Close();
        }

        throw new TimeoutException($"No dead-letter message with correlation id {correlationId} within {Timeout.TotalSeconds}s.");
    }

    public static string? Header(ConsumeResult<byte[], byte[]> result, string name) =>
        result.Message.Headers.TryGetLastBytes(name, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;
}
