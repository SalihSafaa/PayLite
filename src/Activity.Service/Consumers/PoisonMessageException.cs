namespace PayLite.ActivityService;

/// <summary>
/// The message can never be processed, however often we retry it: invalid JSON, missing fields,
/// a version we don't know, an amount that isn't positive. ADR-0004: no retries for these.
/// </summary>
public sealed class PoisonMessageException : Exception
{
    public PoisonMessageException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
