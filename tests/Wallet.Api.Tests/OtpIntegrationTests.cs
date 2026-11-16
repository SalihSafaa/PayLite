using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace PayLite.WalletApi.Tests;

/// <summary>
/// The one-time code on sending money and on revealing the card details (demo code 123456).
/// Every test client sends the right code by default (WalletApiFactory); these tests remove or change it.
/// </summary>
public class OtpIntegrationTests : IClassFixture<WalletApiFactory>
{
    private const string Password = "TestPassword123!";
    private const string OtpHeader = "X-OTP-Code";
    private readonly WalletApiFactory _factory;

    public OtpIntegrationTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(null, "required")]
    [InlineData("654321", "wrong")]
    [InlineData("12345", "wrong")]
    public async Task Transfer_WithoutTheRightCode_Returns403AndMovesNothing(string? otp, string titleContains)
    {
        var alice = await CreateUserAsync(balance: 10_000);
        var bob = await CreateUserAsync();

        var response = await PostTransferAsync(alice.Client, bob.Username, 1_000m, Guid.NewGuid(), otp);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("urn:paylite:otp-invalid", problem!.Type);
        Assert.Contains(titleContains, problem.Title);
        Assert.Equal(10_000m, await GetBalanceAsync(alice.Client));
        Assert.Equal(0m, await GetBalanceAsync(bob.Client));
    }

    [Fact]
    public async Task Transfer_WithTheRightCode_Returns201()
    {
        var alice = await CreateUserAsync(balance: 10_000);
        var bob = await CreateUserAsync();

        var response = await PostTransferAsync(alice.Client, bob.Username, 1_000m, Guid.NewGuid(), WalletApiFactory.Otp);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(9_000m, await GetBalanceAsync(alice.Client));

        // Everything the receipt screen shows.
        var receipt = (await response.Content.ReadFromJsonAsync<TransferResultDto>())!;
        Assert.True(receipt.TransferId > 0);
        Assert.Equal(bob.Username, receipt.ToUsername);
        Assert.Equal(1_000m, receipt.Amount);
        Assert.Equal(9_000m, receipt.NewBalance);
        Assert.Equal(TransferStatus.Completed, receipt.Status);
        Assert.InRange(receipt.CreatedAt, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Transfer_WrongCodeThenRightCode_WithTheSameKey_SendsOnce()
    {
        // The code is checked before the idempotency step, so the failed attempt stores no key (ADR-0002).
        var alice = await CreateUserAsync(balance: 10_000);
        var bob = await CreateUserAsync();
        var key = Guid.NewGuid();

        var wrong = await PostTransferAsync(alice.Client, bob.Username, 1_000m, key, "000000");
        var right = await PostTransferAsync(alice.Client, bob.Username, 1_000m, key, WalletApiFactory.Otp);
        var again = await PostTransferAsync(alice.Client, bob.Username, 1_000m, key, WalletApiFactory.Otp);

        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Created, right.StatusCode);
        Assert.False(right.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.Equal("true", again.Headers.GetValues("Idempotent-Replayed").Single());
        // The replay shows the same transfer, with its original time.
        var first = (await right.Content.ReadFromJsonAsync<TransferResultDto>())!;
        var replay = (await again.Content.ReadFromJsonAsync<TransferResultDto>())!;
        Assert.Equal(first.TransferId, replay.TransferId);
        Assert.Equal(first.CreatedAt, replay.CreatedAt);
        Assert.Equal(9_000m, await GetBalanceAsync(alice.Client)); // moved once
    }

    [Fact]
    public async Task Card_WithoutCode_Returns403()
    {
        var alice = await CreateUserAsync();

        var response = await GetCardAsync(alice.Client, otp: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("urn:paylite:otp-invalid", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Type);
    }

    [Fact]
    public async Task Card_WithCode_ReturnsTheSameDetailsEveryTime_NotCached()
    {
        var alice = await CreateUserAsync();
        var wallet = await alice.Client.GetFromJsonAsync<WalletDto>("/api/wallets/me");

        var first = await GetCardAsync(alice.Client, WalletApiFactory.Otp);
        var card = await first.Content.ReadFromJsonAsync<CardDetailsDto>();
        var second = await (await GetCardAsync(alice.Client, WalletApiFactory.Otp)).Content.ReadFromJsonAsync<CardDetailsDto>();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.True(first.Headers.CacheControl?.NoStore);
        Assert.Equal(alice.Username, card!.HolderName);
        Assert.Matches(@"^\d{4} \d{4} \d{4} \d{4}$", card.Number);
        Assert.Matches(@"^\d{2}/\d{2}$", card.Expiry);
        Assert.Matches(@"^\d{3}$", card.Cvv);
        Assert.EndsWith(wallet!.CardLast4, card.Number.Replace(" ", ""));
        Assert.Equal(card.Number, second!.Number);
        Assert.Equal(card.Cvv, second.Cvv);
    }

    [Fact]
    public async Task Wallet_ShowsOnlyTheLastFourDigits_WithoutACode()
    {
        var alice = await CreateUserAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/wallets/me");
        request.Headers.Remove(OtpHeader);

        var wallet = await (await alice.Client.SendAsync(request)).Content.ReadFromJsonAsync<WalletDto>();

        Assert.Matches(@"^\d{4}$", wallet!.CardLast4);
    }

    // ---------- helpers ----------

    private static Task<HttpResponseMessage> PostTransferAsync(HttpClient client, string toUsername, decimal amount, Guid key, string? otp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
        {
            Content = JsonContent.Create(new { toUsername, amount })
        };
        request.Headers.Add("Idempotency-Key", key.ToString());
        return SendWithOtpAsync(client, request, otp);
    }

    private static Task<HttpResponseMessage> GetCardAsync(HttpClient client, string? otp) =>
        SendWithOtpAsync(client, new HttpRequestMessage(HttpMethod.Get, "/api/wallets/me/card"), otp);

    /// <summary>Sends with exactly this code (or none), whatever the client's default header is.</summary>
    private static async Task<HttpResponseMessage> SendWithOtpAsync(HttpClient client, HttpRequestMessage request, string? otp)
    {
        var saved = client.DefaultRequestHeaders.GetValues(OtpHeader).ToList();
        client.DefaultRequestHeaders.Remove(OtpHeader);
        try
        {
            if (otp is not null)
                request.Headers.Add(OtpHeader, otp);
            return await client.SendAsync(request);
        }
        finally
        {
            client.DefaultRequestHeaders.Add(OtpHeader, saved);
        }
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

        return new TestUser(username, client);
    }

    private sealed record TestUser(string Username, HttpClient Client);
}
