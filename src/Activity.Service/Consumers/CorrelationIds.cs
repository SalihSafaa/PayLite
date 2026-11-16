using System.Text.RegularExpressions;

namespace PayLite.ActivityService;

/// <summary>
/// The correlation id comes from a Kafka header or the event body and ends up in every log line, so it is
/// accepted only in the same shape the Wallet API accepts (<c>^[A-Za-z0-9-]{1,64}$</c>). Anything else
/// (newlines, very long values) would let a bad producer write into our logs.
/// </summary>
public static partial class CorrelationIds
{
    public const string Missing = "-";
    public const string Invalid = "invalid";

    public static string Sanitize(string? value) =>
        string.IsNullOrEmpty(value) ? Missing
        : SafeId().IsMatch(value) ? value
        : Invalid;

    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$")]
    private static partial Regex SafeId();
}
