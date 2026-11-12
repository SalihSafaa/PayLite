namespace PayLite.WalletApi;

/// <summary>
/// One completed money movement. Rows are written ONLY by wallet.usp_Transfer and never
/// updated or deleted, so it has CreatedAt/CreatedBy but no "last update" fields.
/// </summary>
public class Transfer
{
    public long Id { get; set; }
    public int FromWalletId { get; set; }
    public Wallet FromWallet { get; set; } = null!;
    public int ToWalletId { get; set; }
    public Wallet ToWallet { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = WalletDefaults.Currency;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
