using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PayLite.WalletApi.Tests;

public class TransferIntegrationTests : IClassFixture<WalletApiFactory>
{
    private const string Password = "TestPassword123!";
    private readonly WalletApiFactory _factory;

    public TransferIntegrationTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Transfer_Valid_MovesMoneyAndRecordsOneTransfer()
    {
        var alice = await CreateUserAsync(balance: 10_000);
        var bob = await CreateUserAsync();

        var response = await alice.Client.PostAsJsonAsync("/api/transfers", new { toUsername = bob.Username, amount = 2_500 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TransferResultDto>();
        Assert.Equal(7_500m, result!.NewBalance);
        Assert.Equal(7_500m, await GetBalanceAsync(alice.Client));
        Assert.Equal(2_500m, await GetBalanceAsync(bob.Client));
        Assert.Equal(1, await CountTransfersFromAsync(alice.WalletId));
    }

    [Fact]
    public async Task Transfer_InsufficientFunds_Returns409AndChangesNothing()
    {
        var alice = await CreateUserAsync(balance: 1_000);
        var bob = await CreateUserAsync();

        var response = await alice.Client.PostAsJsonAsync("/api/transfers", new { toUsername = bob.Username, amount = 1_001 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1_000m, await GetBalanceAsync(alice.Client));
        Assert.Equal(0m, await GetBalanceAsync(bob.Client));
        Assert.Equal(0, await CountTransfersFromAsync(alice.WalletId));
    }

    [Fact]
    public async Task Transfer_UnknownRecipient_Returns404()
    {
        var alice = await CreateUserAsync(balance: 1_000);

        var response = await alice.Client.PostAsJsonAsync("/api/transfers", new { toUsername = "nobody_by_this_name", amount = 10 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_ToYourself_Returns400()
    {
        var alice = await CreateUserAsync(balance: 1_000);

        var response = await alice.Client.PostAsJsonAsync("/api/transfers", new { toUsername = alice.Username, amount = 10 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1_000m, await GetBalanceAsync(alice.Client));
    }

    [Fact]
    public async Task Transfer_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/transfers", new { toUsername = "someone", amount = 10 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Failure scenario 2: 20 parallel transfers drain a wallet.</summary>
    [Fact]
    public async Task Transfer_20InParallel_ExactlyTenSucceedAndBalanceNeverNegative()
    {
        var alice = await CreateUserAsync(balance: 10_000);
        var bob = await CreateUserAsync();

        var statuses = await FireTogetherAsync(Enumerable.Range(0, 20).Select(_ =>
            (Func<Task<HttpStatusCode>>)(async () =>
                (await alice.Client.PostAsJsonAsync("/api/transfers", new { toUsername = bob.Username, amount = 1_000 })).StatusCode)));

        var summary = string.Join(", ", statuses.GroupBy(s => s).Select(g => $"{(int)g.Key} x{g.Count()}"));
        Assert.True(statuses.Count(s => s == HttpStatusCode.OK) == 10, summary);
        Assert.True(statuses.Count(s => s == HttpStatusCode.Conflict) == 10, summary);
        Assert.Equal(0m, await GetBalanceAsync(alice.Client));
        Assert.Equal(10_000m, await GetBalanceAsync(bob.Client));
        Assert.Equal(10, await CountTransfersFromAsync(alice.WalletId));
    }

    /// <summary>Failure scenario 3: A->B and B->A at the same moment. Lock ordering means no deadlock (1205).</summary>
    [Fact]
    public async Task Transfer_BothDirectionsAtOnce_AllSucceedWithoutDeadlock()
    {
        var alice = await CreateUserAsync(balance: 100_000);
        var bob = await CreateUserAsync(balance: 100_000);

        var aliceToBob = Enumerable.Range(0, 20).Select(_ => (Func<Task<HttpStatusCode>>)(async () =>
            (await alice.Client.PostAsJsonAsync("/api/transfers", new { toUsername = bob.Username, amount = 1_000 })).StatusCode));
        var bobToAlice = Enumerable.Range(0, 20).Select(_ => (Func<Task<HttpStatusCode>>)(async () =>
            (await bob.Client.PostAsJsonAsync("/api/transfers", new { toUsername = alice.Username, amount = 1_000 })).StatusCode));

        var statuses = await FireTogetherAsync(aliceToBob.Concat(bobToAlice));

        var summary = string.Join(", ", statuses.GroupBy(s => s).Select(g => $"{(int)g.Key} x{g.Count()}"));
        Assert.True(statuses.All(s => s == HttpStatusCode.OK), summary);
        Assert.Equal(100_000m, await GetBalanceAsync(alice.Client));
        Assert.Equal(100_000m, await GetBalanceAsync(bob.Client));
        Assert.Equal(20, await CountTransfersFromAsync(alice.WalletId));
        Assert.Equal(20, await CountTransfersFromAsync(bob.WalletId));
    }

    // ---------- helpers ----------

    private sealed record TestUser(string Username, HttpClient Client, int WalletId);

    /// <summary>Releases all requests at the same moment (instead of one by one as tasks are created).</summary>
    private static async Task<HttpStatusCode[]> FireTogetherAsync(IEnumerable<Func<Task<HttpStatusCode>>> requests)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = requests.Select(request => Task.Run(async () =>
        {
            await gate.Task;
            return await request();
        })).ToList();

        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

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

    private static async Task<decimal> GetBalanceAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<WalletDto>("/api/wallets/me"))!.Balance;

    private async Task<int> CountTransfersFromAsync(int walletId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        return await context.Transfers.CountAsync(t => t.FromWalletId == walletId);
    }
}
