using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace PayLite.WalletApi.Tests;

public class HistoryIntegrationTests : IClassFixture<WalletApiFactory>
{
    private const string Password = "TestPassword123!";
    private readonly WalletApiFactory _factory;

    public HistoryIntegrationTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task History_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/transfers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task History_ShowsSentAndReceived_NewestFirst()
    {
        var alice = await CreateUserAsync(balance: 10_000);
        var bob = await CreateUserAsync(balance: 5_000);
        var carol = await CreateUserAsync();

        await SendAsync(alice.Client, bob.Username, 100m);
        await SendAsync(bob.Client, alice.Username, 200m);
        await SendAsync(alice.Client, carol.Username, 300m);

        var page = await GetHistoryAsync(alice.Client);

        Assert.Equal(3, page.TotalCount);
        var items = page.Items.ToList();

        Assert.Equal(TransferDirection.Sent, items[0].Direction);
        Assert.Equal(carol.Username, items[0].CounterpartyUsername);
        Assert.Equal(300m, items[0].Amount);
        Assert.Equal(9_800m, items[0].BalanceAfter);

        Assert.Equal(TransferDirection.Received, items[1].Direction);
        Assert.Equal(bob.Username, items[1].CounterpartyUsername);
        Assert.Equal(200m, items[1].Amount);
        Assert.Null(items[1].BalanceAfter);

        Assert.Equal(TransferDirection.Sent, items[2].Direction);
        Assert.Equal(bob.Username, items[2].CounterpartyUsername);
        Assert.Equal(9_900m, items[2].BalanceAfter);

        Assert.All(items, item => Assert.Equal("IQD", item.Currency));
    }

    [Fact]
    public async Task History_Paging_ReturnsTheRightSlice()
    {
        var alice = await CreateUserAsync(balance: 1_000);
        var bob = await CreateUserAsync();
        for (var amount = 1; amount <= 5; amount++)
            await SendAsync(alice.Client, bob.Username, amount);

        var page2 = await GetHistoryAsync(alice.Client, "?page=2&pageSize=2");
        var page3 = await GetHistoryAsync(alice.Client, "?page=3&pageSize=2");

        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(2, page2.Page);
        Assert.Equal(2, page2.PageSize);
        Assert.Equal(new[] { 3m, 2m }, page2.Items.Select(i => i.Amount));
        Assert.Equal(new[] { 1m }, page3.Items.Select(i => i.Amount));
    }

    [Fact]
    public async Task History_DoesNotShowOtherUsersTransfers()
    {
        var alice = await CreateUserAsync();
        var bob = await CreateUserAsync(balance: 1_000);
        var carol = await CreateUserAsync();
        await SendAsync(bob.Client, carol.Username, 100m);

        var page = await GetHistoryAsync(alice.Client);

        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    public async Task History_InvalidPaging_Returns400(string query)
    {
        var alice = await CreateUserAsync();

        var response = await alice.Client.GetAsync($"/api/transfers{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MerchantPayments_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/merchants/me/payments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MerchantPayments_AsNormalUser_Returns403()
    {
        var alice = await CreateUserAsync();

        var response = await alice.Client.GetAsync("/api/merchants/me/payments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MerchantPayments_ShowsOnlyIncomingPayments_NewestFirst()
    {
        var alice = await CreateUserAsync(balance: 1_000);
        var bob = await CreateUserAsync(balance: 1_000);
        var shop = await CreateUserAsync(accountType: "Merchant");

        await SendAsync(alice.Client, shop.Username, 100m);
        await SendAsync(bob.Client, shop.Username, 200m);
        await SendAsync(shop.Client, alice.Username, 50m); // outgoing: must not be listed

        var response = await shop.Client.GetAsync("/api/merchants/me/payments?page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var page = (await response.Content.ReadFromJsonAsync<PagedResponseDto<MerchantPaymentDto>>())!;

        Assert.Equal(2, page.TotalCount);
        var items = page.Items.ToList();
        Assert.Equal(bob.Username, items[0].FromUsername);
        Assert.Equal(200m, items[0].Amount);
        Assert.Equal(alice.Username, items[1].FromUsername);
        Assert.Equal(100m, items[1].Amount);
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
            (await client.PostAsJsonAsync("/api/wallets/me/top-ups", new { amount = balance })).EnsureSuccessStatusCode();
        }

        return new TestUser(username, client);
    }

    private static async Task SendAsync(HttpClient client, string toUsername, decimal amount)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
        {
            Content = JsonContent.Create(new { toUsername, amount })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<PagedResponseDto<TransferHistoryItemDto>> GetHistoryAsync(HttpClient client, string query = "")
    {
        var response = await client.GetAsync($"/api/transfers{query}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResponseDto<TransferHistoryItemDto>>())!;
    }

    private sealed record TestUser(string Username, HttpClient Client);
}
