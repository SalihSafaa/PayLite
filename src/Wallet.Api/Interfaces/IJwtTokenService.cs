namespace PayLite.WalletApi;

public interface IJwtTokenService
{
    string CreateToken(User user);
}