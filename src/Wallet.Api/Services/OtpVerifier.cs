using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace PayLite.WalletApi;

public interface IOtpVerifier
{
    /// <returns>null when the code is right, otherwise the 403 ProblemDetails to return.</returns>
    ProblemDetails? Check(string? code);
}

/// <summary>Checks the demo one-time code (see <see cref="OtpOptions"/>). The code is never logged.</summary>
public sealed class OtpVerifier : IOtpVerifier
{
    private readonly byte[] _expected;

    public OtpVerifier(IOptions<OtpOptions> options)
    {
        _expected = Encoding.UTF8.GetBytes(options.Value.DemoCode);
    }

    public ProblemDetails? Check(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Problem("A one-time code is required for this action.");

        // Constant-time compare, so the time taken doesn't hint at how many digits were right.
        var right = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(code.Trim()), _expected);
        return right ? null : Problem("The one-time code is wrong.");
    }

    // 403, not 401: the session is fine (401 would make the frontend log the user out); this action needs more.
    private static ProblemDetails Problem(string title) => new()
    {
        Status = StatusCodes.Status403Forbidden,
        Type = OtpOptions.ProblemType,
        Title = title
    };
}
