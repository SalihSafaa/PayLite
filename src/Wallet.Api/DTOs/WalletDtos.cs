using System.ComponentModel.DataAnnotations;

namespace PayLite.WalletApi;

public class WalletDto
{
    public int Id { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsMerchant { get; set; }
}

public class TopUpRequestDto
{
    // The upper limit is configurable (Wallet:MaxTopUpAmount) and checked in WalletService.
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }
}
