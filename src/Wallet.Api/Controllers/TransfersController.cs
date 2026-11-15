using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayLite.Contracts;

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
    [ProducesResponseType<TransferResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransferResultDto>> CreateTransfer(
        CreateTransferDto request,
        [FromHeader(Name = CorrelationHeaders.IdempotencyKey)] string? idempotencyKey)
    {
        if (!Guid.TryParse(idempotencyKey, out var key))
            return this.ToActionResult(Result<TransferOutcome>.Failure(
                ErrorType.Validation, "A valid Idempotency-Key UUID header is required."));

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
