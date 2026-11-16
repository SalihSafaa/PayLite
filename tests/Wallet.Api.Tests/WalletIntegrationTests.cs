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

}
