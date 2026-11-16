using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayLite.Contracts;

namespace PayLite.ActivityService.Tests;

/// <summary>
/// Tests event processing against the real SQL Server used by the integration-test fixture.
/// </summary>
public class TransferEventProcessorTests : IClassFixture<ActivityServiceFactory>
{
    private static readonly DateTime OccurredAt = new(2026, 11, 12, 9, 15, 0, DateTimeKind.Utc);

    private readonly ActivityServiceFactory _factory;

    public TransferEventProcessorTests(ActivityServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TransferToMerchant_WritesNotificationAndSummary()
    {
        var merchantUserId = NewUserId();
        var eventId = Guid.NewGuid();

        var outcome = await ProcessAsync(Event(eventId, merchantUserId, toIsMerchant: true, amount: 25000m));

        Assert.Equal(ProcessOutcome.Processed, outcome);
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
    public async Task TransferToUser_WritesNotificationWithoutSummary()
    {
        var userId = NewUserId();

        await ProcessAsync(Event(Guid.NewGuid(), userId, toIsMerchant: false, amount: 5000m));

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();
        Assert.Equal(1, await context.Notifications.CountAsync(n => n.UserId == userId));
        Assert.False(await context.MerchantHourlySummaries.AnyAsync(s => s.MerchantUserId == userId));
    }

    [Fact]
    public async Task SameEventTwice_SecondIsDuplicate_OnlyAppliesEffectsOnce()
    {
        var merchantUserId = NewUserId();
        var json = Event(Guid.NewGuid(), merchantUserId, toIsMerchant: true, amount: 25000m);

        Assert.Equal(ProcessOutcome.Processed, await ProcessAsync(json));
        Assert.Equal(ProcessOutcome.Duplicate, await ProcessAsync(json));

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();
        Assert.Equal(1, await context.Notifications.CountAsync(n => n.UserId == merchantUserId));
        var summary = await context.MerchantHourlySummaries.SingleAsync(s => s.MerchantUserId == merchantUserId);
        Assert.Equal(1, summary.PaymentCount);
        Assert.Equal(25000m, summary.TotalAmount);
    }

    [Fact]
    public async Task TwoPaymentsInTheSameHour_AreAddedUp()
    {
        var merchantUserId = NewUserId();

        await ProcessAsync(Event(Guid.NewGuid(), merchantUserId, toIsMerchant: true, amount: 25000m));
        await ProcessAsync(Event(Guid.NewGuid(), merchantUserId, toIsMerchant: true, amount: 10000m));

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();
        var summary = await context.MerchantHourlySummaries.SingleAsync(s => s.MerchantUserId == merchantUserId);
        Assert.Equal(2, summary.PaymentCount);
        Assert.Equal(35000m, summary.TotalAmount);
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("")]
    [InlineData("{\"eventType\":\"TransferCompleted\",\"version\":1}")]
    public async Task InvalidMessage_IsPoison(string json)
    {
        await Assert.ThrowsAsync<PoisonMessageException>(() => ProcessAsync(json));
    }

    [Fact]
    public async Task UnknownVersion_IsPoison()
    {
        var json = Event(Guid.NewGuid(), NewUserId(), toIsMerchant: false, amount: 1000m, version: 99);

        await Assert.ThrowsAsync<PoisonMessageException>(() => ProcessAsync(json));
    }

    [Fact]
    public async Task CurrencyOtherThanIqd_IsPoison_AndNothingIsWritten()
    {
        var merchantUserId = NewUserId();
        var json = Event(Guid.NewGuid(), merchantUserId, toIsMerchant: true, amount: 1000m)
            .Replace("\"currency\":\"IQD\"", "\"currency\":\"USD\"");

        var ex = await Assert.ThrowsAsync<PoisonMessageException>(() => ProcessAsync(json));

        Assert.Contains("USD", ex.Message);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();
        Assert.False(await context.Notifications.AnyAsync(n => n.UserId == merchantUserId));
        Assert.False(await context.MerchantHourlySummaries.AnyAsync(s => s.MerchantUserId == merchantUserId));
    }

    [Fact]
    public async Task UnknownEventType_IsIgnored()
    {
        var json = JsonSerializer.Serialize(
            new EventEnvelope<object>(Guid.NewGuid(), "SomethingElse", 1, OccurredAt, "corr", new { }),
            EventJson.Options);

        Assert.Equal(ProcessOutcome.IgnoredEventType, await ProcessAsync(json));
    }

    private static int NewUserId() => Random.Shared.Next(100_000, 999_999);

    private async Task<ProcessOutcome> ProcessAsync(string json)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<TransferEventProcessor>();
        return await processor.ProcessAsync(json, CancellationToken.None);
    }

    private static string Event(
        Guid eventId,
        int toUserId,
        bool toIsMerchant,
        decimal amount,
        int version = TransferCompletedData.CurrentVersion)
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

        return JsonSerializer.Serialize(
            new EventEnvelope<TransferCompletedData>(eventId, EventTypes.TransferCompleted, version, OccurredAt, "corr-test", data),
            EventJson.Options);
    }
}
