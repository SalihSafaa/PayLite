namespace PayLite.ActivityService;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// The Activity Service only VALIDATES tokens issued by the Wallet API.
/// Issuer, Audience and Key must be identical to the Wallet API's Jwt section.
/// </summary>
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
}
