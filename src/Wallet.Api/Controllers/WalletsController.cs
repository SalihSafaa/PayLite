using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PayLite.WalletApi;

[ApiController]
[Route("api/wallets")]
[Authorize]
public class WalletsController : ControllerBase
{
    private readonly IWalletService _walletService;

    public WalletsController(IWalletService walletService)
    {
        _walletService = walletService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<WalletDto>> GetMyWallet()
    {
        var result = await _walletService.GetMyWalletAsync(User.GetUserId());
        if (!result.IsSuccess)
            return this.ToActionResult(result);

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
