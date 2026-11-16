namespace PayLite.WalletApi;

/// <summary>A top-up request, as its owner or an Admin sees it.</summary>
public class TopUpRequestDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    /// <summary>Pending, Approved or Rejected.</summary>
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    /// <summary>The wallet balance right after the credit; only for Approved.</summary>
    public decimal? BalanceAfter { get; set; }
}
