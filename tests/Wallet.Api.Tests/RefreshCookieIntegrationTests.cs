using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PayLite.WalletApi.Tests;

/// <summary>
/// The refresh token as an httpOnly cookie (ADR-0003), so the browser can stay logged in after a page refresh.
/// Cookies are handled by hand here, so each test controls exactly what the "browser" sends.
/// </summary>
public class RefreshCookieIntegrationTests : IClassFixture<WalletApiFactory>
{
    private const string Password = "TestPassword123!";
    private const string CookieName = "paylite_refresh";
    private readonly WalletApiFactory _factory;

    public RefreshCookieIntegrationTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_SetsHttpOnlyStrictCookieScopedToAuth()
    {
        var client = CreateClient();
        var username = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });

        response.EnsureSuccessStatusCode();
        var setCookie = Assert.Single(SetCookieHeaders(response));
        Assert.StartsWith($"{CookieName}=", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=604800", setCookie, StringComparison.OrdinalIgnoreCase); // 7 days
    }

    [Fact]
    public async Task Refresh_WithOnlyTheCookie_ReturnsANewAccessTokenAndRotatesTheCookie()
    {
        var client = CreateClient();
        var firstCookie = await LoginAndGetCookieAsync(client);

        var response = await RefreshWithCookieAsync(client, firstCookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        var newCookie = CookieValue(response);
        Assert.NotNull(newCookie);
        Assert.NotEqual(firstCookie, newCookie);

        // The new access token works on a protected endpoint.
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/wallets/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(me)).StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAnOldCookieAfterRotation_IsRejected_AndRevokesTheNewOneToo()
    {
        var client = CreateClient();
        var stolen = await LoginAndGetCookieAsync(client);
        var rotated = CookieValue(await RefreshWithCookieAsync(client, stolen))!;

        // Someone replays the old token: reuse detection revokes every session of that user.
        var replay = await RefreshWithCookieAsync(client, stolen);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Contains(SetCookieHeaders(replay), c => c.StartsWith($"{CookieName}=;", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshWithCookieAsync(client, rotated)).StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookieOrBody_Returns401()
    {
        var response = await CreateClient().PostAsync("/api/auth/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheCookieTokenAndDeletesTheCookie()
    {
        var client = CreateClient();
        var cookie = await LoginAndGetCookieAsync(client);
        var accessToken = (await (await RefreshWithCookieAsync(client, cookie)).Content.ReadFromJsonAsync<LoginResponseDto>())!.AccessToken;
        cookie = await LatestCookieAsync(client, cookie);

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        logout.Headers.Add("Cookie", $"{CookieName}={cookie}");
        var response = await client.SendAsync(logout);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(SetCookieHeaders(response), c => c.StartsWith($"{CookieName}=;", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshWithCookieAsync(client, cookie)).StatusCode);
    }

    // https so the Secure cookie is realistic; no automatic cookie handling, we send what we choose.
    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, BaseAddress = new Uri("https://localhost") });

    private static async Task<string> RegisterAsync(HttpClient client)
    {
        var username = $"u_{Guid.NewGuid():N}"[..30];
        (await client.PostAsJsonAsync("/api/auth/register", new { username, password = Password })).EnsureSuccessStatusCode();
        return username;
    }

    private static async Task<string> LoginAndGetCookieAsync(HttpClient client)
    {
        var username = await RegisterAsync(client);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        login.EnsureSuccessStatusCode();
        return CookieValue(login) ?? throw new InvalidOperationException("Login set no refresh cookie.");
    }

    // Logout test helper: the refresh above rotated the cookie, so log in again is not needed; just
    // refresh once more to get the value the browser would hold now.
    private static async Task<string> LatestCookieAsync(HttpClient client, string current)
    {
        var response = await RefreshWithCookieAsync(client, current);
        return CookieValue(response) ?? current;
    }

    private static Task<HttpResponseMessage> RefreshWithCookieAsync(HttpClient client, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"{CookieName}={cookie}");
        return client.SendAsync(request);
    }

    private static IEnumerable<string> SetCookieHeaders(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

    private static string? CookieValue(HttpResponseMessage response)
    {
        var header = SetCookieHeaders(response).FirstOrDefault(c => c.StartsWith($"{CookieName}=", StringComparison.Ordinal));
        var value = header?[(CookieName.Length + 1)..].Split(';')[0];
        return string.IsNullOrEmpty(value) ? null : Uri.UnescapeDataString(value);
    }
}
