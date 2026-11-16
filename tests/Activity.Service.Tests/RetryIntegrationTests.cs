using System.Collections.Concurrent;
using System.Text;
using Confluent.Kafka;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static PayLite.ActivityService.Tests.KafkaTestHelper;

namespace PayLite.ActivityService.Tests;

/// <summary>
/// Transient failures (e.g. SQL Server down): 3 retries, then the dead-letter topic, so a partition is
/// never blocked forever. The processor is swapped for one that fails on purpose.
/// </summary>
public class RetryIntegrationTests : IClassFixture<RetryIntegrationTests.FlakyFactory>, IAsyncLifetime
{
    private readonly FlakyFactory _factory;
    private IProducer<byte[], byte[]> _producer = null!;

    public RetryIntegrationTests(FlakyFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await EnsureTopicsAsync(_factory.KafkaBootstrapServers);
        _producer = CreateProducer(_factory.KafkaBootstrapServers);
        _ = _factory.Services;
    }

    public Task DisposeAsync()
    {
        _producer.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task FailsTwiceThenWorks_IsProcessed_NotDeadLettered()
    {
        var eventId = Guid.NewGuid();
        var correlation = $"{FlakyProcessor.FailTwice}-{Guid.NewGuid():N}";

        await ProduceAsync(_producer, "7",
            Encoding.UTF8.GetBytes(TransferCompletedJson(eventId, 7, toIsMerchant: false, 1000m, correlation)), correlation);
        await WaitUntilProcessedAsync(_factory.Services, eventId);

        Assert.Equal(3, FlakyProcessor.Attempts[correlation]); // failed, failed, worked
    }

    [Fact]
    public async Task KeepsFailing_GoesToDeadLetterAfterThreeRetries_AndTheNextEventIsStillProcessed()
    {
        var correlation = $"{FlakyProcessor.FailAlways}-{Guid.NewGuid():N}";
        await ProduceAsync(_producer, "8",
            Encoding.UTF8.GetBytes(TransferCompletedJson(Guid.NewGuid(), 8, toIsMerchant: false, 1000m, correlation)), correlation);

        var nextEventId = Guid.NewGuid();
        await ProduceAsync(_producer, "8",
            Encoding.UTF8.GetBytes(TransferCompletedJson(nextEventId, 8, toIsMerchant: false, 2000m, "after-outage")), "after-outage");

        var deadLetter = ReadDeadLetter(_factory.KafkaBootstrapServers, correlation);
        Assert.Equal(DeadLetterReasons.RetriesExhausted, Header(deadLetter, DeadLetterHeaders.Reason));
        Assert.Equal("4", Header(deadLetter, DeadLetterHeaders.Attempts)); // 1 try + 3 retries
        Assert.Contains("simulated outage", Header(deadLetter, DeadLetterHeaders.Error));
        Assert.Equal(4, FlakyProcessor.Attempts[correlation]);

        await WaitUntilProcessedAsync(_factory.Services, nextEventId); // the partition moved on
    }

    /// <summary>The real service, but with <see cref="FlakyProcessor"/> in front of the real processor.</summary>
    public sealed class FlakyFactory : ActivityServiceFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITransferEventProcessor>();
                services.AddScoped<ITransferEventProcessor, FlakyProcessor>();
            });
        }
    }

    /// <summary>
    /// Fails on purpose when the event's correlation id starts with a marker, otherwise hands over to the
    /// real processor. Counts attempts per correlation id.
    /// </summary>
    public sealed class FlakyProcessor : ITransferEventProcessor
    {
        public const string FailTwice = "fail-twice";
        public const string FailAlways = "fail-always";
        public static readonly ConcurrentDictionary<string, int> Attempts = new();

        private readonly TransferEventProcessor _real;

        public FlakyProcessor(TransferEventProcessor real)
        {
            _real = real;
        }

        public Task<ProcessOutcome> ProcessAsync(string? json, CancellationToken cancellationToken)
        {
            var correlation = ExtractCorrelationId(json);
            if (correlation is not null && (correlation.StartsWith(FailTwice) || correlation.StartsWith(FailAlways)))
            {
                var attempt = Attempts.AddOrUpdate(correlation, 1, (_, n) => n + 1);
                if (correlation.StartsWith(FailAlways) || attempt <= 2)
                    throw new InvalidOperationException("simulated outage (e.g. SQL Server is down)");
            }

            return _real.ProcessAsync(json, cancellationToken);
        }

        private static string? ExtractCorrelationId(string? json)
        {
            if (json is null) return null;
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("correlationId", out var value) ? value.GetString() : null;
        }
    }
}
