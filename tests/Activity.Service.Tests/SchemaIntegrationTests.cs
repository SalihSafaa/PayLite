using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PayLite.ActivityService.Tests;

/// <summary>
/// Proves the database itself enforces the rules the consumer relies on (ADR-0004),
/// against a real SQL Server: these constraints do not exist in the EF InMemory provider.
/// </summary>
public class SchemaIntegrationTests : IClassFixture<ActivityServiceFactory>
{
    private const int UniqueKeyViolation = 2627;
    private const int CheckConstraintViolation = 547;

    private readonly ActivityServiceFactory _factory;

    public SchemaIntegrationTests(ActivityServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProcessedEvents_SameEventIdTwice_FailsWith2627()
    {
        var eventId = Guid.NewGuid();
        await InsertProcessedEventAsync(eventId);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => InsertProcessedEventAsync(eventId));
        Assert.Equal(UniqueKeyViolation, Assert.IsType<SqlException>(ex.InnerException).Number);
    }

    [Fact]
    public async Task Notifications_SameEventIdTwice_FailsAsDuplicate()
    {
        var eventId = Guid.NewGuid();
        await InsertNotificationAsync(eventId);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => InsertNotificationAsync(eventId));
        Assert.Equal(2601, Assert.IsType<SqlException>(ex.InnerException).Number);
    }

    [Fact]
    public async Task MerchantHourlySummaries_HourOutOfRange_IsRejected()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();

        context.MerchantHourlySummaries.Add(new MerchantHourlySummary
        {
            MerchantUserId = 1,
            Date = new DateOnly(2026, 11, 12),
            Hour = 24,
            PaymentCount = 1,
            TotalAmount = 1000m,
            Currency = "IQD",
            LastUpdatedAt = DateTime.UtcNow
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(CheckConstraintViolation, Assert.IsType<SqlException>(ex.InnerException).Number);
    }

    private async Task InsertProcessedEventAsync(Guid eventId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();

        context.ProcessedEvents.Add(new ProcessedEvent
        {
            EventId = eventId,
            EventType = "TransferCompleted",
            ProcessedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private async Task InsertNotificationAsync(Guid eventId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ActivityDbContext>();

        context.Notifications.Add(new Notification
        {
            UserId = 27,
            EventId = eventId,
            TransferId = 1042,
            FromUsername = "ali",
            Amount = 25000m,
            Currency = "IQD",
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }
}
