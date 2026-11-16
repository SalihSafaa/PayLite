using Microsoft.EntityFrameworkCore;

namespace PayLite.ActivityService;

public class MerchantSummaryService : IMerchantSummaryService
{
    private readonly ActivityDbContext _context;

    public MerchantSummaryService(ActivityDbContext context)
    {
        _context = context;
    }

    public DateOnly Today() => BaghdadTime.Today();

    public async Task<DailySummaryDto> GetDailySummaryAsync(int merchantUserId, DateOnly date, CancellationToken cancellationToken)
    {
        var rows = await _context.MerchantHourlySummaries
            .AsNoTracking()
            .Where(s => s.MerchantUserId == merchantUserId && s.Date == date)
            .ToListAsync(cancellationToken);

        // The day is the SUM of its hours: there is only one stored number per hour to keep right.
        var byHour = Enumerable.Range(0, 24)
            .Select(hour =>
            {
                var row = rows.SingleOrDefault(r => r.Hour == hour);
                return new HourlyTotalDto
                {
                    Hour = hour,
                    PaymentCount = row?.PaymentCount ?? 0,
                    TotalAmount = row?.TotalAmount ?? 0m
                };
            })
            .ToList();

        return new DailySummaryDto
        {
            Date = date,
            Currency = rows.FirstOrDefault()?.Currency.Trim() ?? "IQD",
            PaymentCount = byHour.Sum(h => h.PaymentCount),
            TotalAmount = byHour.Sum(h => h.TotalAmount),
            ByHour = byHour
        };
    }
}
