namespace PayLite.Contracts;

/// <summary>
/// Payload of <see cref="EventTypes.TransferCompleted"/>, version 1.
/// </summary>
/// <remarks>
/// Money follows docs/adr/0001-money-representation.md: <c>decimal</c> in C#, <c>decimal(18,2)</c>
/// in SQL Server, a JSON number on the wire, and IQD amounts are always whole dinars.
/// </remarks>
public sealed record TransferCompletedData(
    long TransferId,
    int FromWalletId,
    int ToWalletId,
    bool ToIsMerchant,
    decimal Amount,
    string Currency)
{
    public const int CurrentVersion = 1;
}
