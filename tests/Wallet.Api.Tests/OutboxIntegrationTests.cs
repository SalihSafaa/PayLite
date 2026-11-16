using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayLite.Contracts;

namespace PayLite.WalletApi.Tests;

/// <summary>Transactional outbox + publisher (ADR-0004) and failure scenario 4.</summary>
public class OutboxIntegrationTests : IClassFixture<WalletApiFactory>
{
    private const string Password = "TestPassword123!";
    private readonly WalletApiFactory _factory;

    public OutboxIntegrationTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Transfer_WritesOneOutboxEventWithTheFullEnvelope()
    {
        var alice = await CreateUserAsync(balance: 10_000);
        var bob = await CreateUserAsync(accountType: "Merchant");
        var correlationId = $"test-{Guid.NewGuid():N}";

        var response = await PostTransferAsync(alice.Client, bob.Username, 2_500, correlationId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(correlationId, response.Headers.GetValues(CorrelationHeaders.CorrelationId).Single());
        var transfer = await response.Content.ReadFromJsonAsync<TransferResultDto>();

        var row = Assert.Single(await GetOutboxRowsForRecipientAsync(bob.WalletId));
        Assert.Equal(bob.WalletId.ToString(), row.MessageKey);
        Assert.Equal(Topics.Transfers, row.Topic);
        Assert.Equal(correlationId, row.CorrelationId);

        var envelope = JsonSerializer.Deserialize<EventEnvelope<TransferCompletedData>>(row.Payload, EventJson.Options)!;
        Assert.Equal(row.EventId, envelope.EventId);
        Assert.Equal(EventTypes.TransferCompleted, envelope.EventType);
        Assert.Equal(TransferCompletedData.CurrentVersion, envelope.Version);
        Assert.Equal(correlationId, envelope.CorrelationId);
        Assert.Equal(transfer!.TransferId, envelope.Data.TransferId);
        Assert.Equal(alice.WalletId, envelope.Data.FromWalletId);
        Assert.Equal(alice.Username, envelope.Data.FromUsername);
        Assert.Equal(bob.WalletId, envelope.Data.ToWalletId);
        Assert.Equal(await GetUserIdAsync(alice.Username), envelope.Data.FromUserId);
        Assert.Equal(await GetUserIdAsync(bob.Username), envelope.Data.ToUserId);
        Assert.True(envelope.Data.ToIsMerchant);
        Assert.Equal(2_500m, envelope.Data.Amount);
        Assert.Equal("IQD", envelope.Data.Currency);
    }

    [Fact]
    public async Task ReplayedOrFailedTransfer_WritesNoExtraEvent()
    {
        var alice = await CreateUserAsync(balance: 1_000);
        var bob = await CreateUserAsync();
        var key = Guid.NewGuid();

        (await PostTransferAsync(alice.Client, bob.Username, 500, key: key)).EnsureSuccessStatusCode();
        (await PostTransferAsync(alice.Client, bob.Username, 500, key: key)).EnsureSuccessStatusCode(); // replay
        var failed = await PostTransferAsync(alice.Client, bob.Username, 5_000);                       // insufficient

        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
        Assert.Single(await GetOutboxRowsForRecipientAsync(bob.WalletId));
    }

    [Fact]
    public async Task Publisher_SendsTheEventToKafka_KeyedByRecipientWallet()
    {
        var alice = await CreateUserAsync(balance: 1_000);
        var bob = await CreateUserAsync();
        var correlationId = $"test-{Guid.NewGuid():N}";

        (await PostTransferAsync(alice.Client, bob.Username, 100, correlationId)).EnsureSuccessStatusCode();
        var row = await WaitUntilSentAsync(bob.WalletId, TimeSpan.FromSeconds(30));

        var message = ConsumeUntil(m => m.Key == bob.WalletId.ToString(), TimeSpan.FromSeconds(20));
        Assert.NotNull(message);
        Assert.Equal(row.Payload, message!.Message.Value);
        Assert.Equal(correlationId, Encoding.UTF8.GetString(message.Message.Headers.GetLastBytes(CorrelationHeaders.CorrelationId)));
    }

    /// <summary>
    /// Failure scenario 4: Kafka is down, a transfer is made, then Kafka comes back.
    /// The transfer must succeed immediately; the event must go out once Kafka returns.
    /// </summary>
    [Fact]
    public async Task KafkaDown_TransferSucceedsImmediately_EventIsSentWhenKafkaReturns()
    {
        var alice = await CreateUserAsync(balance: 1_000);
        var bob = await CreateUserAsync();

        await _factory.PauseKafkaAsync();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await PostTransferAsync(alice.Client, bob.Username, 100);
            stopwatch.Stop();

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Transfer took {stopwatch.Elapsed}: it must not wait for Kafka.");
            Assert.Equal(900m, await GetBalanceAsync(alice.Client));

            // /health reports Kafka as Degraded (not Unhealthy): the API keeps serving.
            var health = await _factory.CreateClient().GetAsync("/health");
            using (var body = JsonDocument.Parse(await health.Content.ReadAsStringAsync()))
            {
                Assert.Equal("Degraded", body.RootElement.GetProperty("checks").GetProperty("kafka").GetProperty("status").GetString());
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
            var row = Assert.Single(await GetOutboxRowsForRecipientAsync(bob.WalletId));
            Assert.Null(row.SentAt);
        }
        finally
        {
            await _factory.UnpauseKafkaAsync();
        }

        var sent = await WaitUntilSentAsync(bob.WalletId, TimeSpan.FromSeconds(60));
        Assert.NotNull(sent.SentAt);
        Assert.NotNull(ConsumeUntil(m => m.Key == bob.WalletId.ToString(), TimeSpan.FromSeconds(20)));
    }

    // ---------- helpers ----------

    private sealed record TestUser(string Username, HttpClient Client, int WalletId);

    private static Task<HttpResponseMessage> PostTransferAsync(
        HttpClient client, string toUsername, decimal amount, string? correlationId = null, Guid? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
        {
            Content = JsonContent.Create(new { toUsername, amount })
        };
        request.Headers.Add(CorrelationHeaders.IdempotencyKey, (key ?? Guid.NewGuid()).ToString());
        if (correlationId is not null) request.Headers.Add(CorrelationHeaders.CorrelationId, correlationId);
        return client.SendAsync(request);
    }

    private async Task<List<OutboxMessage>> GetOutboxRowsForRecipientAsync(int toWalletId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        return await context.OutboxMessages.AsNoTracking()
            .Where(m => m.MessageKey == toWalletId.ToString())
            .ToListAsync();
    }

    private async Task<int> GetUserIdAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        return await context.Users.Where(u => u.Username == username).Select(u => u.Id).SingleAsync();
    }

    private async Task<OutboxMessage> WaitUntilSentAsync(int toWalletId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var row = (await GetOutboxRowsForRecipientAsync(toWalletId)).Single();
            if (row.SentAt is not null) return row;
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"Outbox row {row.Id} was not sent within {timeout}.");
            await Task.Delay(250);
        }
    }

    private ConsumeResult<string, string>? ConsumeUntil(Func<Message<string, string>, bool> match, TimeSpan timeout)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _factory.KafkaBootstrapServers,
            GroupId = $"test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();
        consumer.Subscribe(Topics.Transfers);

        var deadline = DateTime.UtcNow + timeout;
        try
        {
            while (DateTime.UtcNow < deadline)
            {
                var result = consumer.Consume(TimeSpan.FromMilliseconds(500));
                if (result?.Message is not null && match(result.Message)) return result;
            }
            return null;
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task<TestUser> CreateUserAsync(decimal balance = 0, string accountType = "User")
    {
        var client = _factory.CreateClient();
        var username = $"u_{Guid.NewGuid():N}"[..30];

        (await client.PostAsJsonAsync("/api/auth/register", new { username, password = Password, accountType })).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        if (balance > 0)
        {
            await TestFunding.TopUpAsync(_factory, client, balance);
        }

        var wallet = await client.GetFromJsonAsync<WalletDto>("/api/wallets/me");
        return new TestUser(username, client, wallet!.Id);
    }

    private static async Task<decimal> GetBalanceAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<WalletDto>("/api/wallets/me"))!.Balance;
}
