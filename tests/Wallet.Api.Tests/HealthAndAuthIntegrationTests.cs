using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PayLite.WalletApi.Tests;

/// <summary>
/// Skeleton smoke tests: prove the containers, migrations, auth and health checks are wired up.
/// Owner A adds the transfer / idempotency / concurrency tests next to these.
/// </summary>
public class HealthAndAuthIntegrationTests : IClassFixture<WalletApiFactory>
{
    private readonly HttpClient _client;

    public HealthAndAuthIntegrationTests(WalletApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_WithSqlServerAndKafkaUp_ReportsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", body.RootElement.GetProperty("checks").GetProperty("sqlserver").GetProperty("status").GetString());
        Assert.Equal("Healthy", body.RootElement.GetProperty("checks").GetProperty("kafka").GetProperty("status").GetString());
    }

    [Fact]
    public async Task RegisterThenLogin_ReturnsAccessAndRefreshTokens()
    {
        var username = $"user_{Guid.NewGuid():N}"[..30];

        var register = await _client.PostAsJsonAsync("/api/auth/register",
            new { username, password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { username, password = "TestPassword123!" });
        login.EnsureSuccessStatusCode();

        var tokens = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.False(string.IsNullOrWhiteSpace(tokens!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
    }

    [Fact]
    public async Task Register_SameUsernameDifferentCase_Returns409()
    {
        var username = $"case_{Guid.NewGuid():N}"[..30];

        var first = await _client.PostAsJsonAsync("/api/auth/register",
            new { username, password = "TestPassword123!" });
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsJsonAsync("/api/auth/register",
            new { username = username.ToUpperInvariant(), password = "TestPassword123!" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
