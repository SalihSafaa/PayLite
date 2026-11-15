namespace PayLite.WalletApi;

/// <summary>Deduplication record for one successful transfer request per user and key.</summary>
public sealed class IdempotencyKey
{
    public int UserId { get; set; }
    public Guid Key { get; set; }
    public byte[] RequestHash { get; set; } = [];
    public long TransferId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
