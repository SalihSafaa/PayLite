namespace PayLite.WalletApi;

/// <summary>
/// A request to add money to a wallet (simulated cash-in). It starts Pending; an Admin approves it (the wallet is
/// credited, once, in the same transaction) or rejects it (nothing moves).
/// </summary>
public class TopUp : AuditableEntity
{
    public int Id { get; set; }
    public int WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;
    public decimal Amount { get; set; }

    /// <summary>One of <see cref="TopUpStatus"/>. CHECK constraint in the database.</summary>
    public string Status { get; set; } = TopUpStatus.Pending;

    /// <summary>The wallet balance right after the credit. Only set once Approved.</summary>
    public decimal? BalanceAfter { get; set; }

    /// <summary>Username of the Admin who approved or rejected it.</summary>
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public static class TopUpStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}
