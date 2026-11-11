using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace PayLite.WalletApi;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly WalletDbContext _context;
    private readonly ILogger<RefreshTokenService> _logger;
    private const int ExpirationDays = 7;

    public RefreshTokenService(WalletDbContext context, ILogger<RefreshTokenService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(User user)
    {
        var (rawToken, _) = await GenerateInternalAsync(user);
        return rawToken;
    }

    public async Task<Result<(User User, string RefreshToken)>> ValidateAndRotateAsync(string rawToken)
    {
        var tokenHash = Hash(rawToken);
        var existing = await _context.RefreshTokens
            .Include(rt => rt.User)
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (existing is null)
        {
            _logger.LogWarning("Refresh attempt with unrecognized token.");
            return Result<(User, string)>.Failure(ErrorType.UnAuthorized, "Invalid refresh token.");
        }

        if (existing.RevokedAt is not null)
        {
            _logger.LogWarning("Reused refresh token detected for user {UserId}. Revoking all sessions.", existing.UserId);
            await RevokeAllForUserAsync(existing.UserId);
            return Result<(User, string)>.Failure(ErrorType.UnAuthorized, "Invalid refresh token. Please log in again.");
        }

        if (existing.ExpiresAt <= DateTime.UtcNow)
        {
            return Result<(User, string)>.Failure(ErrorType.UnAuthorized, "Refresh token has expired. Please log in again.");
        }

        var (newRawToken, newToken) = await GenerateInternalAsync(existing.User);

        existing.RevokedAt = DateTime.UtcNow;
        existing.ReplacedByTokenId = newToken.Id;
        await _context.SaveChangesAsync();

        return Result<(User, string)>.Success((existing.User, newRawToken));
    }

    public async Task RevokeAsync(string rawToken)
    {
        var tokenHash = Hash(rawToken);
        var existing = await _context.RefreshTokens
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (existing is null || existing.RevokedAt is not null)
        {
            return;
        }

        existing.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    private async Task RevokeAllForUserAsync(int userId)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    private async Task<(string rawToken, RefreshToken entity)> GenerateInternalAsync(User user)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(ExpirationDays)
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return (rawToken, refreshToken);
    }

    private static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }
}