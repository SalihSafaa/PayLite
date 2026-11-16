using System.Security.Claims;

namespace PayLite.ActivityService;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The user id the Wallet API put in the token (ClaimTypes.NameIdentifier).</summary>
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("The access token has no valid user id claim.");
    }
}
