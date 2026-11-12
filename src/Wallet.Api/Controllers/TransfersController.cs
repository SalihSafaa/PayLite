using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PayLite.WalletApi;

[ApiController]
[Route("api/transfers")]
[Authorize]
public class TransfersController : ControllerBase
{
    private readonly ITransferService _transferService;

    public TransfersController(ITransferService transferService)
    {
        _transferService = transferService;
    }

    [HttpPost]
    public async Task<ActionResult<TransferResultDto>> CreateTransfer(CreateTransferDto request)
    {
        var username = User.Identity?.Name ?? "unknown";
        var result = await _transferService.CreateTransferAsync(User.GetUserId(), username, request);
        if (!result.IsSuccess)
            return this.ToActionResult(result);

        return Ok(result.Value);
    }
}
