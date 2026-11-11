using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.Kafka;
using Testcontainers.MsSql;

namespace PayLite.ActivityService.Tests;

/// <summary>
/// Starts a real SQL Server and a real Kafka in containers, applies the REAL migrations,
/// and points the Activity Service at them. Shared per test class (IClassFixture).
/// Tests produce events straight onto <see cref="KafkaBootstrapServers"/> to drive the consumer.
/// </summary>
public class ActivityServiceFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private readonly KafkaContainer _kafka =
        new KafkaBuilder("apache/kafka:4.1.0").Build();

    public string SqlConnectionString => _sql.GetConnectionString().Replace("Database=master", "Database=ActivityDb");
    public string KafkaBootstrapServers => _kafka.GetBootstrapAddress();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sql.StartAsync(), _kafka.StartAsync());

        var options = new DbContextOptionsBuilder<ActivityDbContext>()
            .UseSqlServer(SqlConnectionString)
            .Options;

        await using var context = new ActivityDbContext(options);
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
                    d.ServiceType == typeof(DbContextOptions<ActivityDbContext>) ||
                    d.ServiceType == typeof(IDbContextOptionsConfiguration<ActivityDbContext>))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            services.AddDbContext<ActivityDbContext>(options =>
                options.UseSqlServer(SqlConnectionString));
        });

        builder.UseSetting("Jwt:Key", "test-only-signing-key-0000000000000000");
        builder.UseSetting("Kafka:BootstrapServers", KafkaBootstrapServers);
    }
}
