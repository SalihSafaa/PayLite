using Microsoft.EntityFrameworkCore;

namespace PayLite.WalletApi;

public sealed class IdempotencyKeyCleanupService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdempotencyKeyCleanupService> _logger;

    public IdempotencyKeyCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<IdempotencyKeyCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
                int deleted;
                var totalDeleted = 0;
                do
                {
                    deleted = await context.Database.ExecuteSqlRawAsync(
                        "DELETE TOP (1000) FROM wallet.IdempotencyKeys WHERE ExpiresAt < SYSUTCDATETIME()",
                        stoppingToken);
                    totalDeleted += deleted;
                }
                while (deleted == 1000 && !stoppingToken.IsCancellationRequested);

                if (totalDeleted > 0)
                    _logger.LogInformation("Removed {DeletedCount} expired transfer idempotency keys", totalDeleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to clean up expired transfer idempotency keys");
            }

            await Task.Delay(CleanupInterval, stoppingToken);
        }
    }
}
