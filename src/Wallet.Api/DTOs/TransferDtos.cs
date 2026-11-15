using System.ComponentModel.DataAnnotations;

namespace PayLite.WalletApi;

public class CreateTransferDto
{
    [Required(ErrorMessage = "Recipient username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Recipient username must be between 3 and 50 characters long.")]
    public string ToUsername { get; set; } = string.Empty;

    // IQD has no fractional unit; runtime maximum is Wallet:MaxTransferAmount.
    [Range(typeof(decimal), "1", "79228162514264337593543950335", ErrorMessage = "Amount must be at least one whole dinar.")]
    public decimal Amount { get; set; }
}

public class TransferResultDto
{
    public long TransferId { get; set; }
    public string ToUsername { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    /// <summary>The sender's balance after the transfer.</summary>
    public decimal NewBalance { get; set; }
}

public sealed record TransferOutcome(TransferResultDto Transfer, bool Replayed);
