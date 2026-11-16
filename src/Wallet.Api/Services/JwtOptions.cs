namespace PayLite.WalletApi;

using System.ComponentModel.DataAnnotations;
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "JWT signing key is required.")]
    [MinLength(32, ErrorMessage = "JWT signing key must be at least 32 characters.")]
    public string Key { get; set; } = string.Empty;
    [Required(ErrorMessage = "JWT issuer is required.")]
    public string Issuer { get; set; } = string.Empty;
    [Required(ErrorMessage = "JWT audience is required.")]
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 1;

    /// <summary>
    /// Secure flag on the refresh-token cookie. Browsers treat http://localhost as secure, so this stays
    /// true for compose; set it to false only to test over plain http on another host name.
    /// </summary>
    public bool RefreshCookieSecure { get; set; } = true;
}