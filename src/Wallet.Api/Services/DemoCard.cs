using System.Globalization;

namespace PayLite.WalletApi;

/// <summary>
/// The card shown on the wallet. DEMO ONLY: PayLite issues no real cards, so the number, expiry and CVV are
/// derived from the wallet id. They're stable for a wallet, look like a card, and are worth nothing.
/// They're never stored and never logged.
/// </summary>
public static class DemoCard
{
    public static string Number(int walletId)
    {
        // "5" + 15 digits from the wallet id: a 16-digit, card-shaped number, the same every time.
        var digits = ("5" + ((long)walletId * 7_919L + 1_000_003L).ToString(CultureInfo.InvariantCulture).PadLeft(15, '0'))[..16];
        return string.Join(' ', Enumerable.Range(0, 4).Select(i => digits.Substring(i * 4, 4)));
    }

    public static string Last4(int walletId) => Number(walletId)[^4..];

    /// <summary>MM/YY, four years after the wallet was opened.</summary>
    public static string Expiry(DateTime walletCreatedAt) =>
        walletCreatedAt.AddYears(4).ToString("MM/yy", CultureInfo.InvariantCulture);

    public static string Cvv(int walletId) => ((walletId * 37 % 900) + 100).ToString(CultureInfo.InvariantCulture);
}
