using System.Net;
using System.Text.Json;

namespace PayLite.ActivityService.Tests;

/// <summary>
/// Skeleton smoke test: proves the containers, migrations and health checks are wired up.
/// Owner B adds the consumer / duplicate-event / dead-letter tests next to this one.
/// </summary>
public class HealthIntegrationTests : IClassFixture<ActivityServiceFactory>
{
    private readonly HttpClient _client;

    public HealthIntegrationTests(ActivityServiceFactory factory)
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
}
