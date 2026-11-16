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
    /// <summary>When the money moved (UTC). On a replayed request, the original transfer's time.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>Always "Completed": a transfer only exists once the money has moved (see TransferStatus).</summary>
    public string Status { get; set; } = TransferStatus.Completed;
}

public sealed record TransferOutcome(TransferResultDto Transfer, bool Replayed);

public static class TransferDirection
{
    public const string Sent = "Sent";
    public const string Received = "Received";
}

/// <summary>
/// A transfer's status. usp_Transfer moves the money and records the transfer in ONE transaction, so a
/// transfer that exists has always completed: there is no pending or failed transfer row (failed requests
/// return an error and leave nothing behind). The field is in the API so the UI can show it and so a later
/// status (e.g. Reversed) doesn't change the response shape.
/// </summary>
public static class TransferStatus
{
    public const string Completed = "Completed";
}

/// <summary>One row of the caller's transfer history (GET /api/transfers).</summary>
public class TransferHistoryItemDto
{
    public long TransferId { get; set; }
    /// <summary>"Sent" or "Received", seen from the caller's side.</summary>
    public string Direction { get; set; } = string.Empty;
    /// <summary>The other side: the recipient for sent items, the sender for received ones.</summary>
    public string CounterpartyUsername { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    /// <summary>The caller's balance right after a sent transfer; null for received items.</summary>
    public decimal? BalanceAfter { get; set; }
    /// <summary>Always "Completed" today (see <see cref="TransferStatus"/>).</summary>
    public string Status { get; set; } = TransferStatus.Completed;
}

/// <summary>One incoming payment to a merchant (GET /api/merchants/me/payments).</summary>
public class MerchantPaymentDto
{
    public long TransferId { get; set; }
    public string FromUsername { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
