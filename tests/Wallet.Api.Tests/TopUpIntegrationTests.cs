using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace PayLite.WalletApi.Tests;

/// <summary>
/// Top-ups are requests: Pending until an Admin approves (credited once) or rejects (nothing moves).
/// Real SQL Server, real migrations.
/// </summary>
public class TopUpIntegrationTests : IClassFixture<WalletApiFactory>
{
    private const string Password = "TestPassword123!";
    private readonly WalletApiFactory _factory;

    public TopUpIntegrationTests(WalletApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Request_IsPending_AndCreditsNothing()
    {
        var alice = await CreateUserAsync();

        var response = await alice.Client.PostAsJsonAsync("/api/wallets/me/top-ups", new { amount = 50_000 });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var request = (await response.Content.ReadFromJsonAsync<TopUpRequestDto>())!;
        Assert.Equal(TopUpStatus.Pending, request.Status);
        Assert.Equal(50_000m, request.Amount);
        Assert.Equal(alice.Username, request.Username);
        Assert.Null(request.BalanceAfter);
        Assert.Equal(0m, await GetBalanceAsync(alice.Client));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(1_000_001)]
    public async Task Request_OutOfRange_Returns400AndCreatesNothing(decimal amount)
    {
        var alice = await CreateUserAsync();

        var response = await alice.Client.PostAsJsonAsync("/api/wallets/me/top-ups", new { amount });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await alice.Client.GetFromJsonAsync<List<TopUpRequestDto>>("/api/wallets/me/top-ups"))!);
    }

    [Fact]
    public async Task Approve_CreditsTheWallet_AndRecordsWhoAndTheBalance()
    {
        var alice = await CreateUserAsync();
        var request = await RequestAsync(alice.Client, 25_000m);
        var admin = TestFunding.CreateAdminClient(_factory, username: "boss");

        var response = await admin.PostAsync($"/api/admin/top-ups/{request.Id}/approve", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var approved = (await response.Content.ReadFromJsonAsync<TopUpRequestDto>())!;
        Assert.Equal(TopUpStatus.Approved, approved.Status);
        Assert.Equal(25_000m, approved.BalanceAfter);
        Assert.Equal("boss", approved.ReviewedBy);
        Assert.NotNull(approved.ReviewedAt);
        Assert.Equal(25_000m, await GetBalanceAsync(alice.Client));
    }

    [Fact]
    public async Task Reject_CreditsNothing()
    {
        var alice = await CreateUserAsync();
        var request = await RequestAsync(alice.Client, 25_000m);

        var response = await TestFunding.CreateAdminClient(_factory).PostAsync($"/api/admin/top-ups/{request.Id}/reject", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TopUpStatus.Rejected, (await response.Content.ReadFromJsonAsync<TopUpRequestDto>())!.Status);
        Assert.Equal(0m, await GetBalanceAsync(alice.Client));
    }

    [Theory]
    [InlineData("approve", "approve")]
    [InlineData("approve", "reject")]
    [InlineData("reject", "approve")]
    public async Task ReviewingTwice_Returns409_AndCreditsAtMostOnce(string first, string second)
    {
        var alice = await CreateUserAsync();
        var request = await RequestAsync(alice.Client, 10_000m);
        var admin = TestFunding.CreateAdminClient(_factory);

        (await admin.PostAsync($"/api/admin/top-ups/{request.Id}/{first}", null)).EnsureSuccessStatusCode();
        var again = await admin.PostAsync($"/api/admin/top-ups/{request.Id}/{second}", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(first == "approve" ? 10_000m : 0m, await GetBalanceAsync(alice.Client));
    }

    [Fact]
    public async Task TwentyAdminsApproveTheSameRequestAtOnce_ItIsCreditedOnce()
    {
        var alice = await CreateUserAsync();
        var request = await RequestAsync(alice.Client, 1_000m);
        var admins = Enumerable.Range(0, 20).Select(i => TestFunding.CreateAdminClient(_factory, -1 - i, $"admin{i}")).ToList();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = admins.Select(admin => Task.Run(async () =>
        {
            await gate.Task;
            return (await admin.PostAsync($"/api/admin/top-ups/{request.Id}/approve", null)).StatusCode;
        })).ToList();
        gate.SetResult();
        var statuses = await Task.WhenAll(tasks);

        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(19, statuses.Count(s => s == HttpStatusCode.Conflict));
        Assert.Equal(1_000m, await GetBalanceAsync(alice.Client));
    }

    [Fact]
    public async Task TwentyRequestsApprovedInParallel_NoLostUpdates()
    {
        var alice = await CreateUserAsync();
        var requests = new List<TopUpRequestDto>();
        for (var i = 0; i < 20; i++)
            requests.Add(await RequestAsync(alice.Client, 1_000m));
        var admin = TestFunding.CreateAdminClient(_factory);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = requests.Select(r => Task.Run(async () =>
        {
            await gate.Task;
            return (await admin.PostAsync($"/api/admin/top-ups/{r.Id}/approve", null)).StatusCode;
        })).ToList();
        gate.SetResult();
        var statuses = await Task.WhenAll(tasks);

        Assert.All(statuses, s => Assert.Equal(HttpStatusCode.OK, s));
        Assert.Equal(20_000m, await GetBalanceAsync(alice.Client));
    }

    [Fact]
    public async Task AdminEndpoints_ForANormalUser_Return403()
    {
        var alice = await CreateUserAsync();
        var request = await RequestAsync(alice.Client, 1_000m);

        Assert.Equal(HttpStatusCode.Forbidden, (await alice.Client.GetAsync("/api/admin/top-ups")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.Client.PostAsync($"/api/admin/top-ups/{request.Id}/approve", null)).StatusCode);
        Assert.Equal(0m, await GetBalanceAsync(alice.Client));
    }

    [Fact]
    public async Task AnAdminCantApproveTheirOwnRequest()
    {
        var bossUser = await CreateUserAsync();
        var request = await RequestAsync(bossUser.Client, 1_000m);
        // An Admin token for the same user id that made the request.
        var sameUserAsAdmin = TestFunding.CreateAdminClient(_factory, bossUser.UserId, bossUser.Username);

        var response = await sameUserAsAdmin.PostAsync($"/api/admin/top-ups/{request.Id}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0m, await GetBalanceAsync(bossUser.Client));
    }

    [Fact]
    public async Task UnknownRequest_Returns404()
    {
        var response = await TestFunding.CreateAdminClient(_factory).PostAsync("/api/admin/top-ups/999999999/approve", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminQueue_ListsPendingOldestFirst_AndFiltersByStatus()
    {
        var alice = await CreateUserAsync();
        var first = await RequestAsync(alice.Client, 1_001m);
        var second = await RequestAsync(alice.Client, 1_002m);
        var admin = TestFunding.CreateAdminClient(_factory);

        var pending = (await admin.GetFromJsonAsync<List<TopUpRequestDto>>("/api/admin/top-ups?status=Pending&limit=200"))!;
        var mine = pending.Where(r => r.Username == alice.Username).Select(r => r.Id).ToList();
        Assert.Equal(new[] { first.Id, second.Id }, mine);

        (await admin.PostAsync($"/api/admin/top-ups/{first.Id}/approve", null)).EnsureSuccessStatusCode();
        var approved = (await admin.GetFromJsonAsync<List<TopUpRequestDto>>("/api/admin/top-ups?status=Approved&limit=200"))!;
        Assert.Contains(approved, r => r.Id == first.Id);
        Assert.DoesNotContain(approved, r => r.Id == second.Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/top-ups?status=Maybe")).StatusCode);
    }

    [Fact]
    public async Task MyTopUps_ShowEachRequestsStatus_NewestFirst()
    {
        var alice = await CreateUserAsync();
        var approved = await RequestAsync(alice.Client, 1_000m);
        var rejected = await RequestAsync(alice.Client, 2_000m);
        var pending = await RequestAsync(alice.Client, 3_000m);
        var admin = TestFunding.CreateAdminClient(_factory);
        (await admin.PostAsync($"/api/admin/top-ups/{approved.Id}/approve", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/top-ups/{rejected.Id}/reject", null)).EnsureSuccessStatusCode();

        var mine = (await alice.Client.GetFromJsonAsync<List<TopUpRequestDto>>("/api/wallets/me/top-ups"))!;

        Assert.Equal(new[] { pending.Id, rejected.Id, approved.Id }, mine.Select(r => r.Id));
        Assert.Equal(new[] { TopUpStatus.Pending, TopUpStatus.Rejected, TopUpStatus.Approved }, mine.Select(r => r.Status));
    }

    // ---------- helpers ----------

    private static async Task<TopUpRequestDto> RequestAsync(HttpClient client, decimal amount)
    {
        var response = await client.PostAsJsonAsync("/api/wallets/me/top-ups", new { amount });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TopUpRequestDto>())!;
    }

    private static async Task<decimal> GetBalanceAsync(HttpClient client)
        => (await client.GetFromJsonAsync<WalletDto>("/api/wallets/me"))!.Balance;

    private async Task<TestUser> CreateUserAsync()
    {
        var client = _factory.CreateClient();
        var username = $"u_{Guid.NewGuid():N}"[..30];

        (await client.PostAsJsonAsync("/api/auth/register", new { username, password = Password })).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        // The user id is in the token's "nameid" claim (JwtSecurityTokenHandler's short name for NameIdentifier).
        var userId = int.Parse(new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken)
            .Claims.First(c => c.Type == "nameid").Value);
        return new TestUser(username, client, userId);
    }

    private sealed record TestUser(string Username, HttpClient Client, int UserId);
}
