namespace PayLite.WalletApi;

/// <summary>
/// One wallet per user, created together with the user at registration.
/// Whether it is a merchant wallet comes from the owner's role (single source of truth).
/// </summary>
public class Wallet : AuditableEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>decimal(18,2). Can never go below zero (CHECK constraint in the database).</summary>
    public decimal Balance { get; set; }

    /// <summary>ISO 4217 code. Every wallet is IQD for now (see ADR-0001).</summary>
    public string Currency { get; set; } = WalletDefaults.Currency;
}

public static class WalletDefaults
{
    public const string Currency = "IQD";
}