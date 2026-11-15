using System.Text.RegularExpressions;
using PayLite.Contracts;
using Serilog.Context;

namespace PayLite.WalletApi;

/// <summary>
/// Takes the X-Correlation-ID the frontend sends (or creates one), echoes it in the response,
/// and attaches it to every log line written while handling the request. The transfer procedure
/// stores it in the outbox event, so the Activity Service logs the same id.
/// </summary>
public partial class CorrelationIdMiddleware
{
    private const string ItemKey = "CorrelationId";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationHeaders.CorrelationId].ToString();
        // Never trust a client value blindly: it ends up in logs and in Kafka.
        var correlationId = SafeId().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString();

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[CorrelationHeaders.CorrelationId] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    public static string Get(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string id ? id : Guid.NewGuid().ToString();

    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$")]
    private static partial Regex SafeId();
}
