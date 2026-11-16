namespace PayLite.ActivityService;

/// <summary>A merchant's payments for one local day (Asia/Baghdad), built only from events.</summary>
public class DailySummaryDto
{
    public DateOnly Date { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int PaymentCount { get; set; }
    public decimal TotalAmount { get; set; }
    /// <summary>Always 24 entries (hour 0-23), with zeros for hours without payments.</summary>
    public List<HourlyTotalDto> ByHour { get; set; } = [];
}

public class HourlyTotalDto
{
    public int Hour { get; set; }
    public int PaymentCount { get; set; }
    public decimal TotalAmount { get; set; }
}
