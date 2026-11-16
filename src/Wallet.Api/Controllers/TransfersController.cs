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
    private readonly IOtpVerifier _otp;

    public TransfersController(ITransferService transferService, IOtpVerifier otp)
    {
        _transferService = transferService;
        _otp = otp;
    }

    /// <summary>The caller's transfers, newest first. <paramref name="direction"/>: All (default), Sent or Received.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponseDto<TransferHistoryItemDto>>> GetHistory(
        [FromQuery] PageRequestDto paging,
        [FromQuery(Name = "direction")] string? direction = null)
    {
        var result = await _transferService.GetHistoryAsync(User.GetUserId(), paging, direction);
        if (!result.IsSuccess)
            return this.ToActionResult(result);

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType<TransferResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransferResultDto>> CreateTransfer(
        CreateTransferDto request,
        [FromHeader(Name = CorrelationHeaders.IdempotencyKey)] string? idempotencyKey,
        [FromHeader(Name = OtpOptions.HeaderName)] string? otp)
    {
        // Checked BEFORE the idempotency step: a missing or wrong code stores no key and moves nothing,
        // so the client can retry the same transfer (same Idempotency-Key) with the right code (ADR-0002).
        if (_otp.Check(otp) is { } otpProblem)
            return StatusCode(StatusCodes.Status403Forbidden, otpProblem);

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
