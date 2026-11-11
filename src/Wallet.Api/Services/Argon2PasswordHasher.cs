using Isopoh.Cryptography.Argon2;
using Microsoft.Extensions.Options;

namespace PayLite.WalletApi;

public class Argon2PasswordHasher : IPasswordHasher
{
    private readonly PasswordHashOptions _options;

    public Argon2PasswordHasher(IOptions<PasswordHashOptions> options)
    {
        _options = options.Value;
    }

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return Argon2.Hash(
            password,
            _options.TimeCost,
            _options.MemoryCost,
            _options.Lanes,
            Argon2Type.HybridAddressing,
            _options.HashLength);
    }

    public bool Verify(string password, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return Argon2.Verify(passwordHash, password);
    }
}