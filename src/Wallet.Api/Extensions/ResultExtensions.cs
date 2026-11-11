using Microsoft.AspNetCore.Mvc;

namespace PayLite.WalletApi;

public static class ResultExtensions
{
    public static ActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result)
    {
        return result.ErrorType switch
        {
            ErrorType.NotFound => controller.NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = result.ErrorMessage
            }),
            ErrorType.Validation => controller.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = result.ErrorMessage
            }),
            ErrorType.Conflict => controller.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = result.ErrorMessage
            }),
            ErrorType.UnAuthorized => controller.Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = result.ErrorMessage
            }),
            ErrorType.Forbidden => controller.StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = result.ErrorMessage
            }),
            ErrorType.UnprocessableEntity => controller.UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = result.ErrorMessage
            }),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = result.ErrorMessage
            }),
        };
    }
}