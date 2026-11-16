using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace PayLite.WalletApi;

public class TransferService : ITransferService
{
    // Error numbers THROWn by wallet.usp_Transfer.
    private const int InsufficientFunds = 50001;
    private const int WalletNotFound = 50002;
    private const int IdempotencyConflict = 50003;
    private const int InvalidTransfer = 50004;

    private readonly WalletDbContext _context;
    private readonly ILogger<TransferService> _logger;
    private readonly WalletOptions _options;

    public TransferService(WalletDbContext context, IOptions<WalletOptions> options, ILogger<TransferService> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<TransferOutcome>> CreateTransferAsync(int fromUserId, string createdBy, Guid idempotencyKey, string correlationId, CreateTransferDto request)
    {
        var toUsername = request.ToUsername.Trim();
        if (request.Amount % 1 != 0)
            return Result<TransferOutcome>.Failure(ErrorType.Validation, "IQD amounts must be whole dinars.");

        if (request.Amount > _options.MaxTransferAmount)
            return Result<TransferOutcome>.Failure(ErrorType.Validation,
                $"A single transfer cannot exceed {_options.MaxTransferAmount:N0} {WalletDefaults.Currency}.");

        var transferId = new SqlParameter("@TransferId", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var newBalance = new SqlParameter("@NewBalance", SqlDbType.Decimal) { Direction = ParameterDirection.Output, Precision = 18, Scale = 2 };
        var replayed = new SqlParameter("@Replayed", SqlDbType.Bit) { Direction = ParameterDirection.Output };

        try
        {
            await _context.Database.ExecuteSqlRawAsync(
                "EXEC wallet.usp_Transfer @FromUserId, @ToUsername, @Amount, @CreatedBy, @IdempotencyKey, @CorrelationId, @TransferId OUTPUT, @NewBalance OUTPUT, @Replayed OUTPUT",
                new SqlParameter("@FromUserId", fromUserId),
                new SqlParameter("@ToUsername", SqlDbType.NVarChar, 50) { Value = toUsername },
                new SqlParameter("@Amount", SqlDbType.Decimal) { Value = request.Amount, Precision = 18, Scale = 2 },
                new SqlParameter("@CreatedBy", SqlDbType.NVarChar, 256) { Value = createdBy },
                new SqlParameter("@IdempotencyKey", SqlDbType.UniqueIdentifier) { Value = idempotencyKey },
                new SqlParameter("@CorrelationId", SqlDbType.NVarChar, 64) { Value = correlationId },
                transferId,
                newBalance,
                replayed);
        }
        catch (SqlException ex) when (ex.Number == InsufficientFunds)
        {
            return Result<TransferOutcome>.Failure(ErrorType.Conflict, "Insufficient funds.");
        }
        catch (SqlException ex) when (ex.Number == WalletNotFound)
        {
            return Result<TransferOutcome>.Failure(ErrorType.NotFound, $"No wallet found for '{toUsername}'.");
        }
        catch (SqlException ex) when (ex.Number == IdempotencyConflict)
        {
            return Result<TransferOutcome>.Failure(ErrorType.UnprocessableEntity,
                "This Idempotency-Key was already used with a different transfer request.");
        }
        catch (SqlException ex) when (ex.Number == InvalidTransfer)
        {
            return Result<TransferOutcome>.Failure(ErrorType.Validation, ex.Message);
        }

        var wasReplayed = (bool)replayed.Value;
        if (!wasReplayed)
        {
            _logger.LogInformation("Transfer {TransferId}: user {FromUserId} sent {Amount} to {ToUsername}",
                transferId.Value, fromUserId, request.Amount, toUsername);
        }

        return Result<TransferOutcome>.Success(new TransferOutcome(new TransferResultDto
        {
            TransferId = (long)transferId.Value,
            ToUsername = toUsername,
            Amount = request.Amount,
            Currency = WalletDefaults.Currency,
            NewBalance = (decimal)newBalance.Value
        }, wasReplayed));
    }

    public async Task<Result<PagedResponseDto<TransferHistoryItemDto>>> GetHistoryAsync(int userId, PageRequestDto page, string? direction = null)
    {
        // null / "All": both sides. Anything else that isn't Sent or Received is a 400, not a silent "All".
        var onlySent = string.Equals(direction, TransferDirection.Sent, StringComparison.OrdinalIgnoreCase);
        var onlyReceived = string.Equals(direction, TransferDirection.Received, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(direction) && !onlySent && !onlyReceived
            && !string.Equals(direction, "All", StringComparison.OrdinalIgnoreCase))
        {
            return Result<PagedResponseDto<TransferHistoryItemDto>>.Failure(ErrorType.Validation,
                "direction must be All, Sent or Received.");
        }

        var walletId = await GetWalletIdAsync(userId);
        if (walletId is null)
            return Result<PagedResponseDto<TransferHistoryItemDto>>.Failure(ErrorType.NotFound, "Wallet not found.");

        // Sent OR received. SQL Server can serve each side from its own index
        // (FromWalletId, CreatedAt) / (ToWalletId, CreatedAt). Id breaks ties in the same instant.
        var query = onlySent ? _context.Transfers.AsNoTracking().Where(t => t.FromWalletId == walletId)
            : onlyReceived ? _context.Transfers.AsNoTracking().Where(t => t.ToWalletId == walletId)
            : _context.Transfers.AsNoTracking().Where(t => t.FromWalletId == walletId || t.ToWalletId == walletId);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(t => new TransferHistoryItemDto
            {
                TransferId = t.Id,
                Direction = t.FromWalletId == walletId ? TransferDirection.Sent : TransferDirection.Received,
                CounterpartyUsername = t.FromWalletId == walletId ? t.ToWallet.User.Username : t.FromWallet.User.Username,
                Amount = t.Amount,
                Currency = t.Currency,
                CreatedAt = t.CreatedAt,
                BalanceAfter = t.FromWalletId == walletId ? t.FromBalanceAfter : null,
                Status = TransferStatus.Completed
            })
            .ToListAsync();

        return Result<PagedResponseDto<TransferHistoryItemDto>>.Success(new PagedResponseDto<TransferHistoryItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page.Page,
            PageSize = page.PageSize
        });
    }

    public async Task<Result<PagedResponseDto<MerchantPaymentDto>>> GetMerchantPaymentsAsync(int merchantUserId, PageRequestDto page)
    {
        var walletId = await GetWalletIdAsync(merchantUserId);
        if (walletId is null)
            return Result<PagedResponseDto<MerchantPaymentDto>>.Failure(ErrorType.NotFound, "Wallet not found.");

        var query = _context.Transfers.AsNoTracking().Where(t => t.ToWalletId == walletId);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(t => new MerchantPaymentDto
            {
                TransferId = t.Id,
                FromUsername = t.FromWallet.User.Username,
                Amount = t.Amount,
                Currency = t.Currency,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return Result<PagedResponseDto<MerchantPaymentDto>>.Success(new PagedResponseDto<MerchantPaymentDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page.Page,
            PageSize = page.PageSize
        });
    }

    private Task<int?> GetWalletIdAsync(int userId) =>
        _context.Wallets.Where(w => w.UserId == userId).Select(w => (int?)w.Id).SingleOrDefaultAsync();
}
