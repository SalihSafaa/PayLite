namespace PayLite.WalletApi;

public interface ITransferService
{
    Task<Result<TransferOutcome>> CreateTransferAsync(int fromUserId, string createdBy, Guid idempotencyKey, string correlationId, CreateTransferDto request);
    Task<Result<PagedResponseDto<TransferHistoryItemDto>>> GetHistoryAsync(int userId, PageRequestDto page);
    Task<Result<PagedResponseDto<MerchantPaymentDto>>> GetMerchantPaymentsAsync(int merchantUserId, PageRequestDto page);
}
