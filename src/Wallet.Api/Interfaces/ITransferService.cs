namespace PayLite.WalletApi;

public interface ITransferService
{
    Task<Result<TransferResultDto>> CreateTransferAsync(int fromUserId, string createdBy, CreateTransferDto request);
}
