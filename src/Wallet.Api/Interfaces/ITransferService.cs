namespace PayLite.WalletApi;

public interface ITransferService
{
    Task<Result<TransferOutcome>> CreateTransferAsync(int fromUserId, string createdBy, Guid idempotencyKey, string correlationId, CreateTransferDto request);
}
