namespace PayLite.ActivityService;

/// <summary>The local time the merchant summary is bucketed by (ADR-0004). Iraq is UTC+3 all year.</summary>
public static class BaghdadTime
{
    public static readonly TimeZoneInfo Zone =
        TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Baghdad", out var zone) && zone is not null
            ? zone
            : TimeZoneInfo.CreateCustomTimeZone("Asia/Baghdad", TimeSpan.FromHours(3), "Baghdad", "Baghdad");

    public static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone));
}
