using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace PayLite.ActivityService.Tests;

/// <summary>GET /api/notifications and POST /api/notifications/{id}/read, against a real SQL Server.</summary>
public class NotificationsApiTests : IClassFixture<ActivityServiceFactory>
{
    private readonly ActivityServiceFactory _factory;

    public NotificationsApiTests(ActivityServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyMyNotifications_NewestFirst_WithUnreadCount()
    {
        var me = NewUserId();
        var someoneElse = NewUserId();
        await SeedAsync(me, 1000m, new DateTime(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc));
        await SeedAsync(me, 2000m, new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc));
        await SeedAsync(someoneElse, 9999m, new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc));

        var list = await ClientFor(me).GetFromJsonAsync<NotificationListDto>("/api/notifications");

        Assert.Equal(2, list!.Items.Count);
        Assert.Equal([2000m, 1000m], list.Items.Select(n => n.Amount)); // newest first
        Assert.Equal(2, list.UnreadCount);
        Assert.All(list.Items, n => Assert.Null(n.ReadAt));
    }

    [Fact]
    public async Task MarkAsRead_RemovesItFromUnreadOnly_AndLowersTheCount()
    {
        var me = NewUserId();
        var first = await SeedAsync(me, 1000m, DateTime.UtcNow.AddMinutes(-2));
        await SeedAsync(me, 2000m, DateTime.UtcNow.AddMinutes(-1));
        var client = ClientFor(me);

        var response = await client.PostAsync($"/api/notifications/{first}/read", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull((await response.Content.ReadFromJsonAsync<NotificationDto>())!.ReadAt);

        var unread = await client.GetFromJsonAsync<NotificationListDto>("/api/notifications?unreadOnly=true");
        Assert.Single(unread!.Items);
        Assert.Equal(2000m, unread.Items[0].Amount);
        Assert.Equal(1, unread.UnreadCount);
    }

    [Fact]
    public async Task MarkAsRead_Twice_KeepsTheFirstReadAt()
    {
        var me = NewUserId();
        var id = await SeedAsync(me, 1000m, DateTime.UtcNow);
        var client = ClientFor(me);

        var first = await (await client.PostAsync($"/api/notifications/{id}/read", null)).Content.ReadFromJsonAsync<NotificationDto>();
        var second = await (await client.PostAsync($"/api/notifications/{id}/read", null)).Content.ReadFromJsonAsync<NotificationDto>();

        Assert.Equal(first!.ReadAt, second!.ReadAt);
    }

    [Fact]
    public async Task MarkAsRead_SomeoneElsesNotification_Returns404_AndChangesNothing()
    {
        var owner = NewUserId();
        var id = await SeedAsync(owner, 1000m, DateTime.UtcNow);

        var response = await ClientFor(NewUserId()).PostAsync($"/api/notifications/{id}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var ownersList = await ClientFor(owner).GetFromJsonAsync<NotificationListDto>("/api/notifications?unreadOnly=true");
        Assert.Single(ownersList!.Items);
    }

    [Fact]
    public async Task WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenInQueryString_IsRejectedOutsideTheHub()
    {
        // ADR-0003: ?access_token= is accepted on /hubs/notifications only.
        var token = TestTokens.For(NewUserId());

        var response = await _factory.CreateClient().GetAsync($"/api/notifications?access_token={token}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task LimitOutOfRange_Returns400(int limit)
    {
        var response = await ClientFor(NewUserId()).GetAsync($"/api/notifications?limit={limit}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- helpers ----------

    private static int NewUserId() => Random.Shared.Next(100_000, 999_999);

    private HttpClient ClientFor(int userId, string role = "User")
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(userId, role));
        return client;
    }

    private async Task<long> SeedAsync(int userId, decimal amount, DateTime occurredAt)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();
        var notification = new Notification
        {
            UserId = userId,
            EventId = Guid.NewGuid(),
            TransferId = Random.Shared.Next(1, 1_000_000),
            FromUsername = "ali",
            Amount = amount,
            Currency = "IQD",
            OccurredAt = occurredAt,
            CreatedAt = DateTime.UtcNow
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        return notification.Id;
    }
}
