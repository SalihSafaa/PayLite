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
    private readonly IOtpVerifier _otp;

    public WalletsController(IWalletService walletService, ITopUpService topUps, IOtpVerifier otp)
    {
        _walletService = walletService;
        _topUps = topUps;
        _otp = otp;
    }

    [HttpGet("me")]
    public async Task<ActionResult<WalletDto>> GetMyWallet()
    {
        var result = await _walletService.GetMyWalletAsync(User.GetUserId());
        if (!result.IsSuccess)
            return this.ToActionResult(result);

        return Ok(result.Value);
    }

    /// <summary>The full card number, expiry and CVV. Needs a one-time code in the X-OTP-Code header.</summary>
    [HttpGet("me/card")]
    [ProducesResponseType<CardDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CardDetailsDto>> GetMyCard([FromHeader(Name = OtpOptions.HeaderName)] string? otp)
    {
        if (_otp.Check(otp) is { } problem)
            return StatusCode(StatusCodes.Status403Forbidden, problem);

        var result = await _walletService.GetMyCardAsync(User.GetUserId());
        if (!result.IsSuccess)
            return this.ToActionResult(result);

        // Sensitive: don't let a browser or proxy keep a copy.
        Response.Headers.CacheControl = "no-store";
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
