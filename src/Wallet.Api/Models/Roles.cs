namespace PayLite.WalletApi;

/// <summary>
/// Role names put in the JWT. The Activity Service checks the same strings,
/// so keep them identical on both sides.
/// </summary>
public static class Roles
{
    public const string User = "User";
    public const string Merchant = "Merchant";
    public const string Admin = "Admin";
}
