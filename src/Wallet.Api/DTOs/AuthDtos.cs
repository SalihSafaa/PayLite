using System.ComponentModel.DataAnnotations;

namespace PayLite.WalletApi;

public class RegisterRequestDto
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters long.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long and no more than 100 characters.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>"User" or "Merchant". Admin can never be chosen at registration.</summary>
    [AllowedValues(Roles.User, Roles.Merchant, ErrorMessage = "AccountType must be 'User' or 'Merchant'.")]
    public string AccountType { get; set; } = Roles.User;
}

public class LoginRequestDto
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, ErrorMessage = "Username cannot exceed 50 characters.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, ErrorMessage = "Password cannot exceed 100 characters.")]
    public string Password { get; set; } = string.Empty;
}

public class UserResponseDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdate { get; set; }
    public string? CreatedBy { get; set; }
    public string? LastUpdatedBy { get; set; }
}

/// <summary>
/// What UserService returns after login/refresh. Not sent to the client as-is: the refresh token goes into
/// the httpOnly cookie, and only <see cref="AccessTokenResponseDto"/> goes into the response body.
/// </summary>
public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Body of /api/auth/login and /api/auth/refresh. The refresh token is deliberately NOT here: it is only in the
/// httpOnly cookie, so no JavaScript (and no XSS) can ever read it (ADR-0003).
/// </summary>
public class AccessTokenResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
}
public class UserUpdateDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Role is required.")]
    [StringLength(50, ErrorMessage = "Role cannot exceed 50 characters.")]
    public string Role { get; set; } = string.Empty;
}
public class RefreshRequestDto
{
    /// <summary>
    /// Optional: the browser sends no body and the token comes from the httpOnly cookie instead
    /// (ADR-0003). Other clients (and the tests) can still send it here.
    /// </summary>
    [StringLength(88, ErrorMessage = "Refresh token cannot exceed 88 characters.")]
    public string? RefreshToken { get; set; }
}