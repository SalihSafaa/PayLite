using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PayLite.ActivityService;

[ApiController]
[Route("api/merchants")]
[Authorize(Roles = "Merchant")]
public class MerchantsController : ControllerBase
{
    private readonly IMerchantSummaryService _summaries;

    public MerchantsController(IMerchantSummaryService summaries)
    {
        _summaries = summaries;
    }

    /// <summary>
    /// My payments for one local day (Asia/Baghdad): count, total and totals by hour.
    /// <paramref name="date"/> is yyyy-MM-dd; it defaults to today in Baghdad.
    /// </summary>
    [HttpGet("me/daily-summary")]
    public async Task<ActionResult<DailySummaryDto>> GetMyDailySummary([FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        return Ok(await _summaries.GetDailySummaryAsync(User.GetUserId(), date ?? _summaries.Today(), cancellationToken));
    }
}
