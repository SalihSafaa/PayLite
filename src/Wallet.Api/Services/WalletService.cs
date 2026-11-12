using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace PayLite.WalletApi;

public class WalletService : IWalletService
{
    private readonly WalletDbContext _context;
    private readonly WalletOptions _options;
    private readonly ILogger<WalletService> _logger;

    public WalletService(WalletDbContext context, IOptions<WalletOptions> options, ILogger<WalletService> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<WalletDto>> GetMyWalletAsync(int userId)
    {
        var wallet = await _context.Wallets
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .Select(w => new WalletDto
            {
                Id = w.Id,
                Balance = w.Balance,
                Currency = w.Currency,
                IsMerchant = w.User.Role == Roles.Merchant
            })
            .SingleOrDefaultAsync();

        return wallet is null
            ? Result<WalletDto>.Failure(ErrorType.NotFound, "You don't have a wallet.")
            : Result<WalletDto>.Success(wallet);
    }

    public async Task<Result<WalletDto>> TopUpAsync(int userId, TopUpRequestDto request)
    {
        if (request.Amount > _options.MaxTopUpAmount)
        {
            return Result<WalletDto>.Failure(ErrorType.Validation,
                $"A single top-up cannot exceed {_options.MaxTopUpAmount:N0} {WalletDefaults.Currency}.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // One atomic statement: the read-modify-write happens inside SQL Server, so two parallel
        // top-ups can never both read the same old balance (no lost update).
        var rows = await _context.Database.SqlQuery<BalanceRow>($"""
            UPDATE wallet.Wallets
            SET Balance = Balance + {request.Amount},
                LastUpdate = SYSUTCDATETIME()
            OUTPUT inserted.Id AS WalletId, inserted.Balance AS Balance
            WHERE UserId = {userId}
            """).ToListAsync();

        if (rows.Count == 0)
        {
            return Result<WalletDto>.Failure(ErrorType.NotFound, "You don't have a wallet.");
        }

        var row = rows[0];

        // The audit row commits together with the balance change, or not at all.
        _context.TopUps.Add(new TopUp { WalletId = row.WalletId, Amount = request.Amount, BalanceAfter = row.Balance });
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        _logger.LogInformation("Wallet {WalletId} topped up by {Amount}, new balance {Balance}", row.WalletId, request.Amount, row.Balance);

        return await GetMyWalletAsync(userId);
    }

    private sealed class BalanceRow
    {
        public int WalletId { get; set; }
        public decimal Balance { get; set; }
    }
}
