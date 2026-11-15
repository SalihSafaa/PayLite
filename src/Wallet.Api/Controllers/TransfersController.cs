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
    public async Task<ActionResult<TransferResultDto>> CreateTransfer(
        CreateTransferDto request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (!Guid.TryParse(idempotencyKey, out var key))
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "A valid Idempotency-Key UUID header is required."
            });

        var username = User.Identity?.Name ?? "unknown";
        var correlationId = CorrelationIdMiddleware.Get(HttpContext);
        var result = await _transferService.CreateTransferAsync(User.GetUserId(), username, key, correlationId, request);
        if (!result.IsSuccess)
            return this.ToActionResult(result);

        if (result.Value!.Replayed)
            Response.Headers["Idempotent-Replayed"] = "true";

        return StatusCode(StatusCodes.Status201Created, result.Value.Transfer);
    }
}
