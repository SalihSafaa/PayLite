using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace PayLite.ActivityService.Tests;

/// <summary>GET /api/merchants/me/daily-summary?date=, against a real SQL Server.</summary>
public class MerchantSummaryApiTests : IClassFixture<ActivityServiceFactory>
{
    private static readonly DateOnly Day = new(2026, 11, 16);
    private readonly ActivityServiceFactory _factory;

    public MerchantSummaryApiTests(ActivityServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DailySummary_AddsUpTheHours_AndReturnsAll24()
    {
        var merchant = Random.Shared.Next(100_000, 999_999);
        await SeedAsync(merchant, Day, hour: 9, count: 2, total: 30000m);
        await SeedAsync(merchant, Day, hour: 14, count: 1, total: 5000m);
        await SeedAsync(merchant, Day.AddDays(-1), hour: 9, count: 7, total: 70000m); // another day: not counted

        var summary = await ClientFor(merchant, "Merchant")
            .GetFromJsonAsync<DailySummaryDto>($"/api/merchants/me/daily-summary?date={Day:yyyy-MM-dd}");

        Assert.Equal(Day, summary!.Date);
        Assert.Equal(3, summary.PaymentCount);
        Assert.Equal(35000m, summary.TotalAmount);
        Assert.Equal("IQD", summary.Currency);
        Assert.Equal(24, summary.ByHour.Count);
        Assert.Equal(Enumerable.Range(0, 24), summary.ByHour.Select(h => h.Hour));
        Assert.Equal(30000m, summary.ByHour[9].TotalAmount);
        Assert.Equal(2, summary.ByHour[9].PaymentCount);
        Assert.Equal(5000m, summary.ByHour[14].TotalAmount);
        Assert.Equal(0, summary.ByHour[10].PaymentCount);
    }

    [Fact]
    public async Task DailySummary_DayWithoutPayments_IsAllZeros()
    {
        var summary = await ClientFor(Random.Shared.Next(100_000, 999_999), "Merchant")
            .GetFromJsonAsync<DailySummaryDto>($"/api/merchants/me/daily-summary?date={Day:yyyy-MM-dd}");

        Assert.Equal(0, summary!.PaymentCount);
        Assert.Equal(0m, summary.TotalAmount);
        Assert.Equal(24, summary.ByHour.Count);
    }

    [Fact]
    public async Task DailySummary_WithoutDate_IsToday()
    {
        var summary = await ClientFor(Random.Shared.Next(100_000, 999_999), "Merchant")
            .GetFromJsonAsync<DailySummaryDto>("/api/merchants/me/daily-summary");

        var todayInBaghdad = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        Assert.Equal(todayInBaghdad, summary!.Date);
    }

    [Fact]
    public async Task DailySummary_ForANormalUser_Returns403()
    {
        var response = await ClientFor(Random.Shared.Next(100_000, 999_999), "User")
            .GetAsync("/api/merchants/me/daily-summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DailySummary_InvalidDate_Returns400()
    {
        var response = await ClientFor(Random.Shared.Next(100_000, 999_999), "Merchant")
            .GetAsync("/api/merchants/me/daily-summary?date=not-a-date");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient ClientFor(int userId, string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(userId, role));
        return client;
    }

    private async Task SeedAsync(int merchant, DateOnly date, byte hour, int count, decimal total)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();
        context.MerchantHourlySummaries.Add(new MerchantHourlySummary
        {
            MerchantUserId = merchant,
            Date = date,
            Hour = hour,
            PaymentCount = count,
            TotalAmount = total,
            Currency = "IQD",
            LastUpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }
}
