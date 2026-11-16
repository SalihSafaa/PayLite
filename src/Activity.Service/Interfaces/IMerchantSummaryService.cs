namespace PayLite.ActivityService;

public interface IMerchantSummaryService
{
    Task<DailySummaryDto> GetDailySummaryAsync(int merchantUserId, DateOnly date, CancellationToken cancellationToken);

    /// <summary>Today in Baghdad, the business day the summary is bucketed by.</summary>
    DateOnly Today();
}
