namespace PayLite.WalletApi;

public interface IRefreshTokenService
{
    Task<string> GenerateAsync(User user);
    Task<Result<(User User, string RefreshToken)>> ValidateAndRotateAsync(string rawToken);
    Task RevokeAsync(string rawToken);
}