namespace PayLite.Contracts;

/// <summary>
/// Payload of <see cref="EventTypes.TransferCompleted"/>, version 1.
/// </summary>
/// <remarks>
/// TODO (Day 2, ADR-0001): confirm how money is represented. <c>decimal</c> is used here so no
/// precision is lost in C#; the JSON shape and the SQL type must match whatever the ADR decides.
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
