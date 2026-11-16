using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PayLite.WalletApi;

/// <summary>Admin only: the top-up approval queue.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public class AdminController : ControllerBase
{
    private const int MaxLimit = 200;
    private readonly ITopUpService _topUps;

    public AdminController(ITopUpService topUps)
    {
        _topUps = topUps;
    }

    /// <summary>Top-up requests with this status: Pending (default, oldest first), Approved, Rejected or All.</summary>
    [HttpGet("top-ups")]
    public async Task<ActionResult<List<TopUpRequestDto>>> GetTopUps(
        [FromQuery] string? status = TopUpStatus.Pending,
        [FromQuery] int limit = 100)
    {
        if (limit is < 1 or > MaxLimit)
            return this.ToActionResult(Result<List<TopUpRequestDto>>.Failure(ErrorType.Validation, $"limit must be between 1 and {MaxLimit}."));

        var result = await _topUps.ListAsync(status, limit);
        return result.IsSuccess ? Ok(result.Value) : this.ToActionResult(result);
    }

    /// <summary>Approves a Pending request and credits the wallet, once. Already reviewed → 409.</summary>
    [HttpPost("top-ups/{id:int}/approve")]
    [ProducesResponseType<TopUpRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TopUpRequestDto>> Approve(int id)
    {
        var result = await _topUps.ApproveAsync(id, User.GetUserId(), User.Identity?.Name ?? "admin");
        return result.IsSuccess ? Ok(result.Value) : this.ToActionResult(result);
    }

    /// <summary>Rejects a Pending request; nothing is credited. Already reviewed → 409.</summary>
    [HttpPost("top-ups/{id:int}/reject")]
    [ProducesResponseType<TopUpRequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TopUpRequestDto>> Reject(int id)
    {
        var result = await _topUps.RejectAsync(id, User.GetUserId(), User.Identity?.Name ?? "admin");
        return result.IsSuccess ? Ok(result.Value) : this.ToActionResult(result);
    }
}
