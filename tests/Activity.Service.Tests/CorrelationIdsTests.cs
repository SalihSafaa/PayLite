namespace PayLite.ActivityService.Tests;

/// <summary>The correlation id ends up in every log line, so only safe values get through.</summary>
public class CorrelationIdsTests
{
    [Theory]
    [InlineData("b7d4c2e1-0f3a-4e5b-9c8d-1a2b3c4d5e6f")]
    [InlineData("demo-kafka-down-004")]
    public void SafeValue_IsKept(string value)
    {
        Assert.Equal(value, CorrelationIds.Sanitize(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingValue_IsADash(string? value)
    {
        Assert.Equal(CorrelationIds.Missing, CorrelationIds.Sanitize(value));
    }

    [Theory]
    [InlineData("abc\n[12:00:00 INF] fake log line")]
    [InlineData("has spaces")]
    [InlineData("semi;colon")]
    public void UnsafeValue_IsReplaced(string value)
    {
        Assert.Equal(CorrelationIds.Invalid, CorrelationIds.Sanitize(value));
    }

    [Fact]
    public void TooLongValue_IsReplaced()
    {
        Assert.Equal(CorrelationIds.Invalid, CorrelationIds.Sanitize(new string('a', 65)));
    }
}
