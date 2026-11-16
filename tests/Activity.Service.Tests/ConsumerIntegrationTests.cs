using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayLite.Contracts;

namespace PayLite.ActivityService.Tests;

/// <summary>
/// Drives the real Kafka consumer and verifies its effects in the real SQL Server.
/// </summary>
public class ConsumerIntegrationTests : IClassFixture<ActivityServiceFactory>, IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static readonly DateTime OccurredAt = new(2026, 11, 12, 9, 15, 0, DateTimeKind.Utc);

    private readonly ActivityServiceFactory _factory;
    private IProducer<string, string> _producer = null!;

    public ConsumerIntegrationTests(ActivityServiceFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await KafkaTestHelper.EnsureTopicsAsync(_factory.KafkaBootstrapServers);

        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _factory.KafkaBootstrapServers
        }).Build();

        _ = _factory.Services;
    }

    public Task DisposeAsync()
    {
        _producer.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task TransferToMerchant_CreatesNotificationAndSummary()
    {
        var merchantUserId = NewUserId();
        var eventId = Guid.NewGuid();

        await ProduceAsync(eventId, merchantUserId, toIsMerchant: true, amount: 25000m);
        await WaitUntilProcessedAsync(eventId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();

        var notification = await context.Notifications.SingleAsync(n => n.EventId == eventId);
        Assert.Equal(merchantUserId, notification.UserId);
        Assert.Equal(25000m, notification.Amount);
        Assert.Equal("ali", notification.FromUsername);
        Assert.Null(notification.ReadAt);

        var summary = await context.MerchantHourlySummaries.SingleAsync(s => s.MerchantUserId == merchantUserId);
        Assert.Equal(new DateOnly(2026, 11, 12), summary.Date);
        Assert.Equal((byte)12, summary.Hour);
        Assert.Equal(1, summary.PaymentCount);
        Assert.Equal(25000m, summary.TotalAmount);
    }

    [Fact]
    public async Task SameEventTwice_OneNotificationAndNoDoubleCount()
    {
        var merchantUserId = NewUserId();
        var duplicateEventId = Guid.NewGuid();
        var markerEventId = Guid.NewGuid();

        await ProduceAsync(duplicateEventId, merchantUserId, toIsMerchant: true, amount: 25000m);
        await ProduceAsync(duplicateEventId, merchantUserId, toIsMerchant: true, amount: 25000m);
        await ProduceAsync(markerEventId, merchantUserId, toIsMerchant: true, amount: 10000m);
        await WaitUntilProcessedAsync(markerEventId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();

        Assert.Equal(1, await context.Notifications.CountAsync(n => n.EventId == duplicateEventId));
        Assert.Equal(2, await context.Notifications.CountAsync(n => n.UserId == merchantUserId));

        var summary = await context.MerchantHourlySummaries.SingleAsync(s => s.MerchantUserId == merchantUserId);
        Assert.Equal(2, summary.PaymentCount);
        Assert.Equal(35000m, summary.TotalAmount);
    }

    private Task ProduceAsync(Guid eventId, int toUserId, bool toIsMerchant, decimal amount)
    {
        var data = new TransferCompletedData(
            TransferId: 1042,
            FromWalletId: 11,
            FromUserId: 5,
            FromUsername: "ali",
            ToWalletId: 27,
            ToUserId: toUserId,
            ToIsMerchant: toIsMerchant,
            Amount: amount,
            Currency: "IQD");
        var envelope = new EventEnvelope<TransferCompletedData>(
            eventId,
            EventTypes.TransferCompleted,
            TransferCompletedData.CurrentVersion,
            OccurredAt,
            Guid.NewGuid().ToString(),
            data);

        return _producer.ProduceAsync(Topics.Transfers, new Message<string, string>
        {
            Key = data.ToWalletId.ToString(),
            Value = JsonSerializer.Serialize(envelope, EventJson.Options)
        });
    }

    private async Task WaitUntilProcessedAsync(Guid eventId)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();
            if (await context.ProcessedEvents.AnyAsync(e => e.EventId == eventId))
            {
                return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Event {eventId} was not processed within {Timeout.TotalSeconds}s.");
    }

    private static int NewUserId() => Random.Shared.Next(100_000, 999_999);
}
