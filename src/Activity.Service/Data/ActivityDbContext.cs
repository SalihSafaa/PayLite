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

    public DbSet<ProcessedEvent> ProcessedEvents { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<MerchantHourlySummary> MerchantHourlySummaries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventId).ValueGeneratedNever();
            entity.Property(e => e.EventType).HasMaxLength(100);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint("CK_Notifications_Amount_Positive", "[Amount] > 0"));
            entity.Property(n => n.Amount).HasPrecision(18, 2);
            entity.Property(n => n.Currency).HasMaxLength(3).IsFixedLength();
            entity.Property(n => n.FromUsername).HasMaxLength(50);
            entity.HasIndex(n => n.EventId).IsUnique();
            entity.HasIndex(n => new { n.UserId, n.OccurredAt });
            entity.HasIndex(n => new { n.UserId, n.ReadAt });
        });

        modelBuilder.Entity<MerchantHourlySummary>(entity =>
        {
            entity.HasKey(s => new { s.MerchantUserId, s.Date, s.Hour });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_MerchantHourlySummaries_Hour", "[Hour] BETWEEN 0 AND 23");
                table.HasCheckConstraint("CK_MerchantHourlySummaries_Count", "[PaymentCount] >= 0");
                table.HasCheckConstraint("CK_MerchantHourlySummaries_Total", "[TotalAmount] >= 0");
            });
            entity.Property(s => s.TotalAmount).HasPrecision(18, 2);
            entity.Property(s => s.Currency).HasMaxLength(3).IsFixedLength();
        });
    }
}
