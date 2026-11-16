namespace PayLite.WalletApi;

/// <summary>
/// The refresh token as an httpOnly cookie (ADR-0003 stretch goal), so a page refresh doesn't log the user out.
/// </summary>
/// <remarks>
/// HttpOnly: JavaScript (and so an XSS bug) can't read it. SameSite=Strict: the browser never sends it on a
/// request started by another site, which is the CSRF protection. Path=/api/auth: it's only sent to the
/// login/refresh/logout endpoints, never with the other API calls. The access token stays in memory only.
/// </remarks>
public static class RefreshTokenCookie
{
    public const string Name = "paylite_refresh";
    private const string Path = "/api/auth";

    public static void Write(HttpResponse response, string refreshToken, bool secure) =>
        response.Cookies.Append(Name, refreshToken, Options(secure, TimeSpan.FromDays(RefreshTokenService.ExpirationDays)));

    public static void Delete(HttpResponse response, bool secure) =>
        response.Cookies.Delete(Name, Options(secure, maxAge: null));

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static CookieOptions Options(bool secure, TimeSpan? maxAge) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        MaxAge = maxAge,
        IsEssential = true
    };
}
