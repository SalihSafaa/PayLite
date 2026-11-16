using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PayLite.WalletApi;

[ApiController]
[Route("api/wallets")]
[Authorize]
public class WalletsController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly IOtpVerifier _otp;

    public WalletsController(IWalletService walletService, IOtpVerifier otp)
    {
        _walletService = walletService;
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

    [HttpPost("me/top-ups")]
    public async Task<ActionResult<WalletDto>> TopUp(TopUpRequestDto request)
    {
        var result = await _walletService.TopUpAsync(User.GetUserId(), request);
        if (!result.IsSuccess)
            return this.ToActionResult(result);

        return Ok(result.Value);
    }
}
