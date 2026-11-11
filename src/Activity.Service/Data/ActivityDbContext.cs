using Microsoft.EntityFrameworkCore;

namespace PayLite.ActivityService;

/// <summary>
/// ActivityDb is built ONLY from Kafka events. This service never reads WalletDb,
/// not even with a cross-database query.
/// </summary>
public class ActivityDbContext : DbContext
{
    public const string Schema = "activity";

    public ActivityDbContext(DbContextOptions<ActivityDbContext> options) : base(options)
    {
    }

    // TODO (Owner B): ProcessedEvents (EventId as PRIMARY KEY), Notifications, MerchantDailySummaries

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
    }
}
