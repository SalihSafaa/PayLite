namespace PayLite.ActivityService;

/// <summary>
/// The merchant daily summary read model, stored per hour.
/// One row per merchant, per local day (Asia/Baghdad), per hour (0-23).
/// The daily count and total are the SUM of that day's rows, so there is only one number to keep right.
/// Updated only with increments (count += 1, total += amount), so the order events arrive in does not matter.
/// </summary>
public class MerchantHourlySummary
{
    /// <summary>
    /// The merchant's user id (the event's toUserId, the JWT's NameIdentifier).
    /// </summary>
    public int MerchantUserId { get; set; }

    /// <summary>
    /// The local business day in Asia/Baghdad.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// 0-23, local time in Asia/Baghdad.
    /// </summary>
    public byte Hour { get; set; }

    public int PaymentCount { get; set; }

    /// <summary>
    /// decimal(18,2), the same type as the Wallet API (ADR-0001).
    /// </summary>
    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = string.Empty;
    public DateTime LastUpdatedAt { get; set; }
}
