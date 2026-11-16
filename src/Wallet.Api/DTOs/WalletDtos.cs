using System.ComponentModel.DataAnnotations;

namespace PayLite.WalletApi;

public class WalletDto
{
    public int Id { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsMerchant { get; set; }
    /// <summary>Last four digits of the wallet's card, safe to show without a one-time code.</summary>
    public string CardLast4 { get; set; } = string.Empty;
}

/// <summary>The full card details. Only returned with a valid one-time code (GET /api/wallets/me/card).</summary>
public class CardDetailsDto
{
    public string HolderName { get; set; } = string.Empty;
    /// <summary>16 digits in groups of four.</summary>
    public string Number { get; set; } = string.Empty;
    /// <summary>MM/YY.</summary>
    public string Expiry { get; set; } = string.Empty;
    public string Cvv { get; set; } = string.Empty;
}

public class TopUpRequestDto
{
    // IQD has no fractional unit; the maximum (Wallet:MaxTopUpAmount) is checked in WalletService.
    [Range(typeof(decimal), "1", "79228162514264337593543950335", ErrorMessage = "Amount must be at least one whole dinar.")]
    public decimal Amount { get; set; }
}
