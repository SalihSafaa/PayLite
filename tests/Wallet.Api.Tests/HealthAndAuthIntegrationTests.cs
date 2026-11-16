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
    public async Task RegisterThenLogin_ReturnsAccessToken_AndRefreshTokenOnlyInTheCookie()
    {
        var username = $"user_{Guid.NewGuid():N}"[..30];

        var register = await _client.PostAsJsonAsync("/api/auth/register",
            new { username, password = "TestPassword123!" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { username, password = "TestPassword123!" });
        login.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("accessToken").GetString()));
        // ADR-0003: the refresh token is never readable by JavaScript, so it is not in the body at all.
        Assert.False(body.RootElement.TryGetProperty("refreshToken", out _));
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("paylite_refresh=", StringComparison.Ordinal));
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
