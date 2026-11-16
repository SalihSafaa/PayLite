using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PayLite.ActivityService.Tests;

/// <summary>
/// Mints access tokens the same way the Wallet API's JwtTokenService does (same claims, handler, issuer,
/// audience, and the test signing key), so the Activity Service can be tested without the Wallet API.
/// </summary>
public static class TestTokens
{
    public const string Key = "test-only-signing-key-0000000000000000";

    public static string For(int userId, string role = "User", string? username = null)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username ?? $"user{userId}"),
            new(ClaimTypes.Role, role)
        ];

        var token = new JwtSecurityToken(
            issuer: "PayLite",
            audience: "PayLite",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
