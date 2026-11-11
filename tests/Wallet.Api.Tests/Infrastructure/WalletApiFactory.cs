using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Kafka;
using Testcontainers.MsSql;

namespace PayLite.WalletApi.Tests;

/// <summary>
/// Starts a real SQL Server and a real Kafka in containers, applies the REAL migrations,
/// and points the Wallet API at them. Shared per test class (IClassFixture), because
/// starting SQL Server per test is far too slow.
/// </summary>
public class WalletApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private readonly KafkaContainer _kafka =
        new KafkaBuilder("apache/kafka:4.1.0").Build();

    public string SqlConnectionString => _sql.GetConnectionString().Replace("Database=master", "Database=WalletDb");
    public string KafkaBootstrapServers => _kafka.GetBootstrapAddress();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sql.StartAsync(), _kafka.StartAsync());

        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseSqlServer(SqlConnectionString)
            .Options;

        await using var context = new WalletDbContext(options);
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(_sql.DisposeAsync().AsTask(), _kafka.DisposeAsync().AsTask());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<WalletDbContext>) ||
                    d.ServiceType == typeof(IDbContextOptionsConfiguration<WalletDbContext>))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            services.AddDbContext<WalletDbContext>(options =>
                options.UseSqlServer(SqlConnectionString));
        });

        builder.UseSetting("Jwt:Key", TestJwt.Key);
        builder.UseSetting("Kafka:BootstrapServers", KafkaBootstrapServers);
    }
}
