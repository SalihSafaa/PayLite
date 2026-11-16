using System.Text;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayLite.Contracts;
using static PayLite.ActivityService.Tests.KafkaTestHelper;

namespace PayLite.ActivityService.Tests;

/// <summary>
/// Failure scenario 7: a poison message goes to paylite.transfers.dlt and the consumer keeps going.
/// Real Kafka + real SQL Server.
/// </summary>
public class DeadLetterIntegrationTests : IClassFixture<ActivityServiceFactory>, IAsyncLifetime
{
    private readonly ActivityServiceFactory _factory;
    private IProducer<byte[], byte[]> _producer = null!;

    public DeadLetterIntegrationTests(ActivityServiceFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await EnsureTopicsAsync(_factory.KafkaBootstrapServers);
        _producer = CreateProducer(_factory.KafkaBootstrapServers);
        _ = _factory.Services; // starts the host, and with it the consumer
    }

    public Task DisposeAsync()
    {
        _producer.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task InvalidJson_GoesToDeadLetterTopic_AndTheNextEventIsStillProcessed()
    {
        var userId = Random.Shared.Next(100_000, 999_999);
        var key = userId.ToString();
        var poisonCorrelation = $"poison-{Guid.NewGuid():N}";
        var poisonValue = Encoding.UTF8.GetBytes("this is not json");

        await ProduceAsync(_producer, key, poisonValue, poisonCorrelation);

        // Same key, so the same partition, right behind the poison message.
        var nextEventId = Guid.NewGuid();
        await ProduceAsync(_producer, key,
            Encoding.UTF8.GetBytes(TransferCompletedJson(nextEventId, userId, toIsMerchant: false, 5000m, "after-poison")),
            "after-poison");

        await WaitUntilProcessedAsync(_factory.Services, nextEventId); // the consumer didn't get stuck

        var deadLetter = ReadDeadLetter(_factory.KafkaBootstrapServers, poisonCorrelation);
        Assert.Equal(poisonValue, deadLetter.Message.Value);                  // original bytes, unchanged
        Assert.Equal(Encoding.UTF8.GetBytes(key), deadLetter.Message.Key);    // original key
        Assert.Equal(DeadLetterReasons.Poison, Header(deadLetter, DeadLetterHeaders.Reason));
        Assert.Equal(Topics.Transfers, Header(deadLetter, DeadLetterHeaders.OriginalTopic));
        Assert.NotNull(Header(deadLetter, DeadLetterHeaders.OriginalPartition));
        Assert.NotNull(Header(deadLetter, DeadLetterHeaders.OriginalOffset));
        Assert.NotNull(Header(deadLetter, DeadLetterHeaders.FailedAt));
        Assert.Contains("JSON", Header(deadLetter, DeadLetterHeaders.Error));

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();
        Assert.Equal(1, await context.Notifications.CountAsync(n => n.UserId == userId)); // only the good event
    }

    [Fact]
    public async Task BytesThatAreNotUtf8_GoToDeadLetterTopic()
    {
        var correlation = $"binary-{Guid.NewGuid():N}";
        byte[] notUtf8 = [0xC3, 0x28, 0xFF, 0xFE];

        await ProduceAsync(_producer, "1", notUtf8, correlation);

        var deadLetter = ReadDeadLetter(_factory.KafkaBootstrapServers, correlation);
        Assert.Equal(notUtf8, deadLetter.Message.Value);
        Assert.Equal(DeadLetterReasons.Poison, Header(deadLetter, DeadLetterHeaders.Reason));
        Assert.Contains("UTF-8", Header(deadLetter, DeadLetterHeaders.Error));
    }

    [Fact]
    public async Task UnknownVersion_GoesToDeadLetterTopic()
    {
        var correlation = $"v99-{Guid.NewGuid():N}";
        var json = TransferCompletedJson(Guid.NewGuid(), 1, toIsMerchant: false, 1000m, correlation)
            .Replace("\"version\":1", "\"version\":99");

        await ProduceAsync(_producer, "1", Encoding.UTF8.GetBytes(json), correlation);

        var deadLetter = ReadDeadLetter(_factory.KafkaBootstrapServers, correlation);
        Assert.Equal(DeadLetterReasons.Poison, Header(deadLetter, DeadLetterHeaders.Reason));
        Assert.Contains("version 99", Header(deadLetter, DeadLetterHeaders.Error));
    }
}
