namespace PayLite.WalletApi;

/// <summary>Audit row for every simulated cash-in, written in the same transaction as the balance change.</summary>
public class TopUp : AuditableEntity
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
}
