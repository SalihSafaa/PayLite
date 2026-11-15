using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PayLite.WalletApi;

[ApiController]
[Route("api/merchants")]
[Authorize(Roles = Roles.Merchant)]
public class MerchantsController : ControllerBase
{
    private readonly ITransferService _transferService;

    public MerchantsController(ITransferService transferService)
    {
        _transferService = transferService;
    }

    /// <summary>Incoming payments to the merchant's wallet, newest first.</summary>
    [HttpGet("me/payments")]
    public async Task<ActionResult<PagedResponseDto<MerchantPaymentDto>>> GetMyPayments([FromQuery] PageRequestDto paging)
    {
        var result = await _transferService.GetMerchantPaymentsAsync(User.GetUserId(), paging);
        if (!result.IsSuccess)
            return this.ToActionResult(result);

        return Ok(result.Value);
    }
}
