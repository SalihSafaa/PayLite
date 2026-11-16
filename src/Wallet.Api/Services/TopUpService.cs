using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace PayLite.WalletApi;

/// <summary>
/// Top-ups are requests: the user asks, an Admin approves (the wallet is credited) or rejects (nothing moves).
/// </summary>
public class TopUpService : ITopUpService
{
    private readonly WalletDbContext _context;
    private readonly WalletOptions _options;
    private readonly ILogger<TopUpService> _logger;

    public TopUpService(WalletDbContext context, IOptions<WalletOptions> options, ILogger<TopUpService> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<TopUpRequestDto>> RequestAsync(int userId, CreateTopUpDto request)
    {
        if (request.Amount % 1 != 0)
            return Result<TopUpRequestDto>.Failure(ErrorType.Validation, "IQD amounts must be whole dinars.");

        if (request.Amount > _options.MaxTopUpAmount)
            return Result<TopUpRequestDto>.Failure(ErrorType.Validation,
                $"A single top-up cannot exceed {_options.MaxTopUpAmount:N0} {WalletDefaults.Currency}.");

        var walletId = await _context.Wallets.Where(w => w.UserId == userId).Select(w => (int?)w.Id).SingleOrDefaultAsync();
        if (walletId is null)
            return Result<TopUpRequestDto>.Failure(ErrorType.NotFound, "You don't have a wallet.");

        var topUp = new TopUp { WalletId = walletId.Value, Amount = request.Amount, Status = TopUpStatus.Pending };
        _context.TopUps.Add(topUp);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Top-up {TopUpId} of {Amount} requested for wallet {WalletId}", topUp.Id, topUp.Amount, walletId);
        return Result<TopUpRequestDto>.Success((await QueryAsync(t => t.Id == topUp.Id, newestFirst: true, 1)).Single());
    }

    public async Task<Result<List<TopUpRequestDto>>> GetMineAsync(int userId, int limit) =>
        Result<List<TopUpRequestDto>>.Success(await QueryAsync(t => t.Wallet.UserId == userId, newestFirst: true, limit));

    public async Task<Result<List<TopUpRequestDto>>> ListAsync(string? status, int limit)
    {
        if (string.IsNullOrEmpty(status) || string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            return Result<List<TopUpRequestDto>>.Success(await QueryAsync(_ => true, newestFirst: true, limit));

        var normalized = new[] { TopUpStatus.Pending, TopUpStatus.Approved, TopUpStatus.Rejected }
            .FirstOrDefault(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
        if (normalized is null)
            return Result<List<TopUpRequestDto>>.Failure(ErrorType.Validation, "status must be All, Pending, Approved or Rejected.");

        // The approval queue is first come, first served.
        return Result<List<TopUpRequestDto>>.Success(
            await QueryAsync(t => t.Status == normalized, newestFirst: normalized != TopUpStatus.Pending, limit));
    }

    /// <summary>
    /// Credits the wallet ONCE, even if two admins (or a double-click) approve at the same moment.
    /// In one transaction:
    /// 1. credit the wallet joined to the request, but only while the request is Pending. The request row is
    ///    read WITH (UPDLOCK), so a second approver waits here until the first commits, then finds it no longer
    ///    Pending and matches 0 rows (409). The increment is a single UPDATE: no read-modify-write.
    /// 2. mark the request Approved with the balance from step 1.
    /// </summary>
    public async Task<Result<TopUpRequestDto>> ApproveAsync(int topUpId, int adminUserId, string adminUsername)
    {
        if (await IsOwnRequestAsync(topUpId, adminUserId))
            return Result<TopUpRequestDto>.Failure(ErrorType.Forbidden, "You can't approve your own top-up.");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var credited = await _context.Database.SqlQuery<CreditedWallet>($"""
            UPDATE w
            SET w.Balance = w.Balance + t.Amount, w.LastUpdate = SYSUTCDATETIME(), w.LastUpdatedBy = {adminUsername}
            OUTPUT inserted.Id AS WalletId, inserted.Balance AS Balance
            FROM wallet.Wallets AS w
            JOIN wallet.TopUps AS t WITH (UPDLOCK, ROWLOCK) ON t.WalletId = w.Id
            WHERE t.Id = {topUpId} AND t.Status = 'Pending'
            """).ToListAsync();

        if (credited.Count == 0)
        {
            await transaction.RollbackAsync();
            return await NotPendingAsync(topUpId);
        }

        var balanceAfter = credited[0].Balance;
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE wallet.TopUps
            SET Status = 'Approved', BalanceAfter = {balanceAfter}, ReviewedBy = {adminUsername}, ReviewedAt = SYSUTCDATETIME(),
                LastUpdate = SYSUTCDATETIME(), LastUpdatedBy = {adminUsername}
            WHERE Id = {topUpId} AND Status = 'Pending'
            """);
        await transaction.CommitAsync();

        _logger.LogInformation("Top-up {TopUpId} approved by {Admin}: wallet {WalletId} balance {Balance}",
            topUpId, adminUsername, credited[0].WalletId, balanceAfter);
        return Result<TopUpRequestDto>.Success((await QueryAsync(t => t.Id == topUpId, newestFirst: true, 1)).Single());
    }

    public async Task<Result<TopUpRequestDto>> RejectAsync(int topUpId, int adminUserId, string adminUsername)
    {
        if (await IsOwnRequestAsync(topUpId, adminUserId))
            return Result<TopUpRequestDto>.Failure(ErrorType.Forbidden, "You can't review your own top-up.");

        // Same guard as approving: only a Pending request can be rejected, and only once.
        var rows = await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE wallet.TopUps
            SET Status = 'Rejected', ReviewedBy = {adminUsername}, ReviewedAt = SYSUTCDATETIME(),
                LastUpdate = SYSUTCDATETIME(), LastUpdatedBy = {adminUsername}
            WHERE Id = {topUpId} AND Status = 'Pending'
            """);

        if (rows == 0)
            return await NotPendingAsync(topUpId);

        _logger.LogInformation("Top-up {TopUpId} rejected by {Admin}", topUpId, adminUsername);
        return Result<TopUpRequestDto>.Success((await QueryAsync(t => t.Id == topUpId, newestFirst: true, 1)).Single());
    }

    private Task<bool> IsOwnRequestAsync(int topUpId, int adminUserId) =>
        _context.TopUps.AnyAsync(t => t.Id == topUpId && t.Wallet.UserId == adminUserId);

    private async Task<Result<TopUpRequestDto>> NotPendingAsync(int topUpId)
    {
        var status = await _context.TopUps.Where(t => t.Id == topUpId).Select(t => t.Status).SingleOrDefaultAsync();
        return status is null
            ? Result<TopUpRequestDto>.Failure(ErrorType.NotFound, "Top-up request not found.")
            : Result<TopUpRequestDto>.Failure(ErrorType.Conflict, $"This top-up was already {status.ToLowerInvariant()}.");
    }

    private Task<List<TopUpRequestDto>> QueryAsync(System.Linq.Expressions.Expression<Func<TopUp, bool>> filter, bool newestFirst, int limit)
    {
        var query = _context.TopUps.AsNoTracking().Where(filter);
        query = newestFirst
            ? query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            : query.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id);

        return query.Take(limit).Select(t => new TopUpRequestDto
        {
            Id = t.Id,
            Username = t.Wallet.User.Username,
            Amount = t.Amount,
            Currency = t.Wallet.Currency,
            Status = t.Status,
            RequestedAt = t.CreatedAt,
            ReviewedAt = t.ReviewedAt,
            ReviewedBy = t.ReviewedBy,
            BalanceAfter = t.BalanceAfter
        }).ToListAsync();
    }

    private sealed class CreditedWallet
    {
        public int WalletId { get; set; }
        public decimal Balance { get; set; }
    }
}
