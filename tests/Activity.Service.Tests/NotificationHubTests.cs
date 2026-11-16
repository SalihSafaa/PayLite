using System.Net;
using System.Text;
using Confluent.Kafka;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using static PayLite.ActivityService.Tests.KafkaTestHelper;

namespace PayLite.ActivityService.Tests;

/// <summary>
/// /hubs/notifications: JWT auth (including ?access_token= on this path), and the live push after the
/// consumer commits a notification. Real Kafka + real SQL Server; the hub runs on the test server.
/// </summary>
public class NotificationHubTests : IClassFixture<ActivityServiceFactory>, IAsyncLifetime
{
    private readonly ActivityServiceFactory _factory;
    private IProducer<byte[], byte[]> _producer = null!;

    public NotificationHubTests(ActivityServiceFactory factory)
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
    public async Task Negotiate_WithTokenInQueryString_IsAccepted()
    {
        // This is how the browser's WebSocket connection authenticates (ADR-0003).
        var token = TestTokens.For(Random.Shared.Next(100_000, 999_999));

        var response = await _factory.CreateClient()
            .PostAsync($"{NotificationHub.Path}/negotiate?negotiateVersion=1&access_token={token}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Negotiate_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().PostAsync($"{NotificationHub.Path}/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NewTransfer_IsPushedLive_ToTheRecipientOnly()
    {
        var recipient = Random.Shared.Next(100_000, 999_999);
        var bystander = Random.Shared.Next(100_000, 999_999);

        var received = new TaskCompletionSource<NotificationDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bystanderGotOne = false;

        await using var recipientConnection = Connect(recipient);
        recipientConnection.On<NotificationDto>(NotificationHub.NotificationMethod, n => received.TrySetResult(n));
        await recipientConnection.StartAsync();

        await using var bystanderConnection = Connect(bystander);
        bystanderConnection.On<NotificationDto>(NotificationHub.NotificationMethod, _ => bystanderGotOne = true);
        await bystanderConnection.StartAsync();

        var eventId = Guid.NewGuid();
        await ProduceAsync(_producer, recipient.ToString(),
            Encoding.UTF8.GetBytes(TransferCompletedJson(eventId, recipient, toIsMerchant: false, 7000m, "live-push")),
            "live-push");

        var notification = await received.Task.WaitAsync(KafkaTestHelper.Timeout);
        Assert.Equal(7000m, notification.Amount);
        Assert.Equal("ali", notification.FromUsername);
        Assert.True(notification.Id > 0);
        Assert.Null(notification.ReadAt);

        await Task.Delay(500); // give a wrong push the chance to arrive
        Assert.False(bystanderGotOne);
    }

    private HubConnection Connect(int userId) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, NotificationHub.Path.TrimStart('/')), options =>
            {
                // The test server has no real sockets: long polling over its in-memory handler.
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(TestTokens.For(userId));
            })
            .Build();
}
