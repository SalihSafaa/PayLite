using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace PayLite.WalletApi.Tests;

public class IdempotencyIntegrationTests : IClassFixture<WalletApiFactory>
{
    private const string Password = "TestPassword123!";
    private readonly WalletApiFactory _factory;

    public IdempotencyIntegrationTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Transfer_WithSameKeyAndSameRequest_ReturnsOriginalResultAndReplayHeader()
    {
        var alice = await CreateUserAsync(balance: 10_000);
        var bob = await CreateUserAsync();
        var key = Guid.NewGuid();

        var first = await PostTransferAsync(alice.Client, bob.Username, 2_500m, key);
        var second = await PostTransferAsync(alice.Client, bob.Username, 2_500m, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.True(second.Headers.TryGetValues("Idempotent-Replayed", out var value));
        Assert.Equal("true", value.Single().ToLowerInvariant());

        var firstResult = await first.Content.ReadFromJsonAsync<TransferResultDto>();
        var secondResult = await second.Content.ReadFromJsonAsync<TransferResultDto>();
        Assert.Equal(firstResult!.TransferId, secondResult!.TransferId);
        Assert.Equal(firstResult.ToUsername, secondResult.ToUsername);
        Assert.Equal(firstResult.Amount, secondResult.Amount);
        Assert.Equal(firstResult.NewBalance, secondResult.NewBalance);
        Assert.Equal(7_500m, await GetBalanceAsync(alice.Client));
        Assert.Equal(2_500m, await GetBalanceAsync(bob.Client));
    }

    [Fact]
    public async Task Transfer_WithSameKeyAndDifferentRequest_Returns422()
    {
        var alice = await CreateUserAsync(balance: 10_000);
        var bob = await CreateUserAsync();
        var key = Guid.NewGuid();

        var first = await PostTransferAsync(alice.Client, bob.Username, 1_500m, key);
        var second = await PostTransferAsync(alice.Client, bob.Username, 2_000m, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        Assert.Equal(8_500m, await GetBalanceAsync(alice.Client));
        Assert.Equal(1_500m, await GetBalanceAsync(bob.Client));
    }

    [Fact]
    public async Task Transfer_ParallelDuplicatesWithSameKey_OnlyOneCreatesTransfer()
    {
        var alice = await CreateUserAsync(balance: 10_000);
        var bob = await CreateUserAsync();
        var key = Guid.NewGuid();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => PostTransferAsync(alice.Client, bob.Username, 1_000m, key))
            .ToArray();

        var responses = await Task.WhenAll(tasks);
        Assert.Equal(10, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(9_000m, await GetBalanceAsync(alice.Client));
        Assert.Equal(1_000m, await GetBalanceAsync(bob.Client));
    }

    [Fact]
    public async Task Transfer_FailedRequestDoesNotConsumeKeyAndCanSucceedAfterTopUp()
    {
        var alice = await CreateUserAsync(balance: 500);
        var bob = await CreateUserAsync();
        var key = Guid.NewGuid();

        var failed = await PostTransferAsync(alice.Client, bob.Username, 1_000m, key);
        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);

        (await alice.Client.PostAsJsonAsync("/api/wallets/me/top-ups", new { amount = 1_000m })).EnsureSuccessStatusCode();
        var retried = await PostTransferAsync(alice.Client, bob.Username, 1_000m, key);

        Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
        Assert.Equal(500m, await GetBalanceAsync(alice.Client));
        Assert.Equal(1_000m, await GetBalanceAsync(bob.Client));
    }

    [Fact]
    public async Task Transfer_SameKeyCanBeUsedByDifferentUsers()
    {
        var alice = await CreateUserAsync(balance: 2_000);
        var bob = await CreateUserAsync(balance: 2_000);
        var recipient = await CreateUserAsync();
        var key = Guid.NewGuid();

        var aliceTransfer = await PostTransferAsync(alice.Client, recipient.Username, 500m, key);
        var bobTransfer = await PostTransferAsync(bob.Client, recipient.Username, 700m, key);

        Assert.Equal(HttpStatusCode.Created, aliceTransfer.StatusCode);
        Assert.Equal(HttpStatusCode.Created, bobTransfer.StatusCode);
        Assert.Equal(1_500m, await GetBalanceAsync(alice.Client));
        Assert.Equal(1_300m, await GetBalanceAsync(bob.Client));
        Assert.Equal(1_200m, await GetBalanceAsync(recipient.Client));
    }

    [Fact]
    public async Task Transfer_WithoutIdempotencyHeader_Returns400()
    {
        var alice = await CreateUserAsync(balance: 2_000);
        var bob = await CreateUserAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
        {
            Content = JsonContent.Create(new { toUsername = bob.Username, amount = 100m })
        };

        var response = await alice.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_WithInvalidIdempotencyHeader_Returns400()
    {
        var alice = await CreateUserAsync(balance: 2_000);
        var bob = await CreateUserAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
        {
            Content = JsonContent.Create(new { toUsername = bob.Username, amount = 100m })
        };
        request.Headers.Add("Idempotency-Key", "not-a-guid");

        var response = await alice.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_WithInvalidAmount_Returns400()
    {
        var alice = await CreateUserAsync(balance: 2_000);
        var bob = await CreateUserAsync();

        var response = await PostTransferAsync(alice.Client, bob.Username, 0m);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Task<HttpResponseMessage> PostTransferAsync(HttpClient client, string toUsername, decimal amount, Guid? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
        {
            Content = JsonContent.Create(new { toUsername, amount })
        };

        request.Headers.Add("Idempotency-Key", (idempotencyKey ?? Guid.NewGuid()).ToString());
        return client.SendAsync(request);
    }

    private static async Task<decimal> GetBalanceAsync(HttpClient client)
        => (await client.GetFromJsonAsync<WalletDto>("/api/wallets/me"))!.Balance;

    private async Task<TestUser> CreateUserAsync(decimal balance = 0)
    {
        var client = _factory.CreateClient();
        var username = $"u_{Guid.NewGuid():N}"[..30];

        (await client.PostAsJsonAsync("/api/auth/register", new { username, password = Password })).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        if (balance > 0)
        {
            (await client.PostAsJsonAsync("/api/wallets/me/top-ups", new { amount = balance })).EnsureSuccessStatusCode();
        }

        var wallet = await client.GetFromJsonAsync<WalletDto>("/api/wallets/me");
        return new TestUser(username, client, wallet!.Id);
    }

    private sealed record TestUser(string Username, HttpClient Client, int WalletId);
}
