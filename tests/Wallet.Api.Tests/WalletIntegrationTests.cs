using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PayLite.WalletApi.Tests;

public class WalletIntegrationTests : IClassFixture<WalletApiFactory>
{
    private const string Password = "TestPassword123!";
    private readonly WalletApiFactory _factory;

    public WalletIntegrationTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_CreatesAnEmptyWallet()
    {
        var client = await CreateLoggedInClientAsync("User");

        var wallet = await client.GetFromJsonAsync<WalletDto>("/api/wallets/me");

        Assert.Equal(0m, wallet!.Balance);
        Assert.Equal("IQD", wallet.Currency);
        Assert.False(wallet.IsMerchant);
    }

    [Fact]
    public async Task RegisterAsMerchant_WalletIsMerchant()
    {
        var client = await CreateLoggedInClientAsync("Merchant");

        var wallet = await client.GetFromJsonAsync<WalletDto>("/api/wallets/me");

        Assert.True(wallet!.IsMerchant);
    }

    [Fact]
    public async Task Register_AsAdmin_Returns400()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { username = NewUsername(), password = Password, accountType = "Admin" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMyWallet_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/wallets/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TopUp_Valid_IncreasesBalanceAndWritesOneAuditRow()
    {
        var client = await CreateLoggedInClientAsync("User");

        var response = await client.PostAsJsonAsync("/api/wallets/me/top-ups", new { amount = 50000 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wallet = await response.Content.ReadFromJsonAsync<WalletDto>();
        Assert.Equal(50000m, wallet!.Balance);
        Assert.Equal(1, await CountTopUpsAsync(wallet.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(1_000_001)]
    public async Task TopUp_OutOfRange_Returns400AndChangesNothing(decimal amount)
    {
        var client = await CreateLoggedInClientAsync("User");

        var response = await client.PostAsJsonAsync("/api/wallets/me/top-ups", new { amount });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var wallet = await client.GetFromJsonAsync<WalletDto>("/api/wallets/me");
        Assert.Equal(0m, wallet!.Balance);
        Assert.Equal(0, await CountTopUpsAsync(wallet.Id));
    }

    [Fact]
    public async Task TopUp_20InParallel_NoLostUpdates()
    {
        var client = await CreateLoggedInClientAsync("User");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await gate.Task;
            return (await client.PostAsJsonAsync("/api/wallets/me/top-ups", new { amount = 1000 })).StatusCode;
        })).ToList();

        gate.SetResult();
        var statuses = await Task.WhenAll(tasks);

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
        var wallet = await client.GetFromJsonAsync<WalletDto>("/api/wallets/me");
        Assert.Equal(20000m, wallet!.Balance);
        Assert.Equal(20, await CountTopUpsAsync(wallet.Id));
    }

    // ---------- helpers ----------

    private static string NewUsername() => $"u_{Guid.NewGuid():N}"[..30];

    private async Task<HttpClient> CreateLoggedInClientAsync(string accountType)
    {
        var client = _factory.CreateClient();
        var username = NewUsername();

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { username, password = Password, accountType });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<LoginResponseDto>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }

    private async Task<int> CountTopUpsAsync(int walletId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        return await context.TopUps.CountAsync(t => t.WalletId == walletId);
    }
}
