namespace PayLite.WalletApi;

public sealed class PasswordHashOptions
{
    public const string SectionName = "PasswordHashing";

    public int TimeCost { get; set; } = 3;
    public int MemoryCost { get; set; } = 65536;
    public int Lanes { get; set; } = 4;
    public int HashLength { get; set; } = 32;
}
