namespace PayLite.WalletApi;

public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdate { get; set; }
    public string? CreatedBy { get; set; }
    public string? LastUpdatedBy { get; set; }
}
