using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace PayLite.WalletApi;

public class WalletDbContext : DbContext
{
    public const string Schema = "wallet";

    private readonly IHttpContextAccessor? _httpContextAccessor;

    public WalletDbContext(
        DbContextOptions<WalletDbContext> options,
        IHttpContextAccessor? httpContextAccessor = null) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    // TODO (Owner A): Wallets, Transfers, IdempotencyKeys, Outbox

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Every table lives in the "wallet" schema, so wallet_app can be granted
        // EXECUTE on the schema instead of on each procedure one by one.
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(user => user.Username).HasMaxLength(50);
            entity.Property(user => user.PasswordHash).HasMaxLength(512);
            entity.Property(user => user.Role).HasMaxLength(50);
            entity.Property(user => user.CreatedBy).HasMaxLength(256);
            entity.Property(user => user.LastUpdatedBy).HasMaxLength(256);
            // Unique + case-insensitive thanks to the default collation (proved by a test).
            entity.HasIndex(user => user.Username).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.Property(token => token.TokenHash).HasMaxLength(44);
            entity.Property(token => token.CreatedBy).HasMaxLength(256);
            entity.Property(token => token.LastUpdatedBy).HasMaxLength(256);
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasOne(token => token.User).WithMany()
                .HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override int SaveChanges()
    {
        return SaveChanges(acceptAllChangesOnSuccess: true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SetAuditFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        SetAuditFields();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void SetAuditFields()
    {
        var now = DateTime.UtcNow;
        var user = _httpContextAccessor?.HttpContext?.User;
        var actor = user?.Identity?.IsAuthenticated == true
            ? user.Identity.Name ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            : "anonymous";

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.LastUpdate = now;
                entry.Entity.CreatedBy = actor;
                entry.Entity.LastUpdatedBy = actor;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(entity => entity.CreatedAt).IsModified = false;
                entry.Property(entity => entity.CreatedBy).IsModified = false;
                entry.Entity.LastUpdate = now;
                entry.Entity.LastUpdatedBy = actor;
            }
        }
    }
}
