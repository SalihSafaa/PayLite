using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace PayLite.WalletApi.Tests;

/// <summary>
/// Puts money in a test wallet the way the app does: the user requests a top-up, an Admin approves it.
/// </summary>
public static class TestFunding
{
    /// <summary>Requests a top-up as the client's user and approves it as a test Admin.</summary>
    public static async Task TopUpAsync(WalletApiFactory factory, HttpClient client, decimal amount)
    {
        var request = await client.PostAsJsonAsync("/api/wallets/me/top-ups", new { amount });
        request.EnsureSuccessStatusCode();
        var topUp = await request.Content.ReadFromJsonAsync<TopUpRequestDto>();

        var approve = await CreateAdminClient(factory).PostAsync($"/api/admin/top-ups/{topUp!.Id}/approve", null);
        approve.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// A client with an Admin access token, minted by the API's own JwtTokenService (same claims, issuer,
    /// audience and key as a real login). The user id doesn't exist, so it never owns the request it approves.
    /// </summary>
    public static HttpClient CreateAdminClient(WalletApiFactory factory, int adminUserId = -1, string username = "test-admin")
    {
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .CreateToken(new User { Id = adminUserId, Username = username, Role = Roles.Admin });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
