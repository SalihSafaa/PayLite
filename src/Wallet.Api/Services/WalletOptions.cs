namespace PayLite.WalletApi;

public sealed class WalletOptions
{
    public const string SectionName = "Wallet";

    /// <summary>The "sane limit" on one simulated cash-in.</summary>
    public decimal MaxTopUpAmount { get; set; } = 1_000_000m;

    /// <summary>Maximum amount for a single transfer.</summary>
    public decimal MaxTransferAmount { get; set; } = 10_000_000m;
}
