using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace PayLite.WalletApi;

public class TransferService : ITransferService
{
    // Error numbers THROWn by wallet.usp_Transfer.
    private const int InsufficientFunds = 50001;
    private const int WalletNotFound = 50002;
    private const int InvalidTransfer = 50003;

    private readonly WalletDbContext _context;
    private readonly ILogger<TransferService> _logger;

    public TransferService(WalletDbContext context, ILogger<TransferService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<TransferResultDto>> CreateTransferAsync(int fromUserId, string createdBy, CreateTransferDto request)
    {
        var toUsername = request.ToUsername.Trim();
        var transferId = new SqlParameter("@TransferId", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
        var newBalance = new SqlParameter("@NewBalance", SqlDbType.Decimal) { Direction = ParameterDirection.Output, Precision = 18, Scale = 2 };

        try
        {
            await _context.Database.ExecuteSqlRawAsync(
                "EXEC wallet.usp_Transfer @FromUserId, @ToUsername, @Amount, @CreatedBy, @TransferId OUTPUT, @NewBalance OUTPUT",
                new SqlParameter("@FromUserId", fromUserId),
                new SqlParameter("@ToUsername", SqlDbType.NVarChar, 50) { Value = toUsername },
                new SqlParameter("@Amount", SqlDbType.Decimal) { Value = request.Amount, Precision = 18, Scale = 2 },
                new SqlParameter("@CreatedBy", SqlDbType.NVarChar, 256) { Value = createdBy },
                transferId,
                newBalance);
        }
        catch (SqlException ex) when (ex.Number == InsufficientFunds)
        {
            return Result<TransferResultDto>.Failure(ErrorType.Conflict, "Insufficient funds.");
        }
        catch (SqlException ex) when (ex.Number == WalletNotFound)
        {
            return Result<TransferResultDto>.Failure(ErrorType.NotFound, $"No wallet found for '{toUsername}'.");
        }
        catch (SqlException ex) when (ex.Number == InvalidTransfer)
        {
            return Result<TransferResultDto>.Failure(ErrorType.Validation, ex.Message);
        }

        _logger.LogInformation("Transfer {TransferId}: user {FromUserId} sent {Amount} to {ToUsername}",
            transferId.Value, fromUserId, request.Amount, toUsername);

        return Result<TransferResultDto>.Success(new TransferResultDto
        {
            TransferId = (long)transferId.Value,
            ToUsername = toUsername,
            Amount = request.Amount,
            Currency = WalletDefaults.Currency,
            NewBalance = (decimal)newBalance.Value
        });
    }
}
