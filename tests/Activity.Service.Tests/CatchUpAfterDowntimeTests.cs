using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static PayLite.ActivityService.Tests.KafkaTestHelper;

namespace PayLite.ActivityService.Tests;

/// <summary>
/// Failure scenario 6: the Activity Service is down while transfers happen, then starts.
/// The events are produced BEFORE the host (and so the consumer) starts; once it starts it must catch up
/// on every one of them: nothing lost, nothing doubled.
/// </summary>
public class CatchUpAfterDowntimeTests : IClassFixture<ActivityServiceFactory>
{
    private readonly ActivityServiceFactory _factory;

    public CatchUpAfterDowntimeTests(ActivityServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EventsPublishedWhileTheServiceIsDown_AreAllProcessedOnceItStarts()
    {
        var merchantUserId = Random.Shared.Next(100_000, 999_999);
        var eventIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        // "The service is down": nothing has touched _factory.Services yet, so the consumer isn't running.
        await EnsureTopicsAsync(_factory.KafkaBootstrapServers);
        using (var producer = CreateProducer(_factory.KafkaBootstrapServers))
        {
            for (var i = 0; i < eventIds.Count; i++)
            {
                var correlation = $"down-{i}";
                var json = TransferCompletedJson(eventIds[i], merchantUserId, toIsMerchant: true, amount: 1000m * (i + 1), correlation);
                await ProduceAsync(producer, merchantUserId.ToString(), Encoding.UTF8.GetBytes(json), correlation);
            }
        }

        // "The service starts": the host and the consumer start now, with a brand-new consumer group.
        var services = _factory.Services;
        foreach (var eventId in eventIds)
            await WaitUntilProcessedAsync(services, eventId);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();
        Assert.Equal(5, await context.Notifications.CountAsync(n => n.UserId == merchantUserId));

        var summary = await context.MerchantHourlySummaries.SingleAsync(s => s.MerchantUserId == merchantUserId);
        Assert.Equal(5, summary.PaymentCount);
        Assert.Equal(15000m, summary.TotalAmount); // 1,000 + 2,000 + 3,000 + 4,000 + 5,000
    }
}
