using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PayLite.WalletApi;

[ApiController]
[Route("api/wallets")]
[Authorize]
public class WalletsController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly ITopUpService _topUps;

    public WalletsController(IWalletService walletService, ITopUpService topUps)
    {
        _walletService = walletService;
        _topUps = topUps;
    }

    [HttpGet("me")]
    public async Task<ActionResult<WalletDto>> GetMyWallet()
    {
        var result = await _walletService.GetMyWalletAsync(User.GetUserId());
        if (!result.IsSuccess)
            return this.ToActionResult(result);

        return Ok(result.Value);
    }

    /// <summary>
    /// Asks for a top-up. Nothing is credited yet: the request is Pending until an Admin approves it,
    /// so the answer is 202 Accepted with the request, not the new balance.
    /// </summary>
    [HttpPost("me/top-ups")]
    [ProducesResponseType<TopUpRequestDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TopUpRequestDto>> RequestTopUp(CreateTopUpDto request)
    {
        var result = await _topUps.RequestAsync(User.GetUserId(), request);
        if (!result.IsSuccess)
            return this.ToActionResult(result);

        return StatusCode(StatusCodes.Status202Accepted, result.Value);
    }

    /// <summary>My top-up requests with their status (Pending / Approved / Rejected), newest first.</summary>
    [HttpGet("me/top-ups")]
    public async Task<ActionResult<List<TopUpRequestDto>>> GetMyTopUps()
    {
        var result = await _topUps.GetMineAsync(User.GetUserId(), limit: 20);
        return result.IsSuccess ? Ok(result.Value) : this.ToActionResult(result);
    }
}
