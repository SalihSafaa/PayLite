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
}
