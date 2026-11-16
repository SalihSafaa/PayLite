namespace PayLite.WalletApi;

public interface IWalletService
{
    Task<Result<WalletDto>> GetMyWalletAsync(int userId);
    Task<Result<CardDetailsDto>> GetMyCardAsync(int userId);
}
