using System.Text.Json;

namespace PayLite.Contracts;

/// <summary>
/// The one set of JSON options used to serialize and deserialize events on both sides,
/// so the producer and the consumer can never disagree about casing.
/// </summary>
public static class EventJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
