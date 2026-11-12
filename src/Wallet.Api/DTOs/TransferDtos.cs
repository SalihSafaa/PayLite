using System.ComponentModel.DataAnnotations;

namespace PayLite.WalletApi;

public class CreateTransferDto
{
    [Required(ErrorMessage = "Recipient username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Recipient username must be between 3 and 50 characters long.")]
    public string ToUsername { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "Amount must be greater than zero.")]
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
