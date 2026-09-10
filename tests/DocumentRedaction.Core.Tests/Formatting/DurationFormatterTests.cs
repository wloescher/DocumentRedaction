using DocumentRedaction.Core.Formatting;

namespace DocumentRedaction.Core.Tests.Formatting;

public class DurationFormatterTests
{
    [Theory]
    [InlineData(0, "0s")]
    [InlineData(0.4, "0s")]
    [InlineData(59.4, "59s")]
    [InlineData(59.6, "1m 0s")]
    [InlineData(60, "1m 0s")]
    [InlineData(90, "1m 30s")]
    [InlineData(3599, "59m 59s")]
    [InlineData(3600, "1h 0m 0s")]
    [InlineData(3661, "1h 1m 1s")]
    [InlineData(86399, "23h 59m 59s")]
    [InlineData(86400, "1d 0h 0m 0s")]
    [InlineData(90061, "1d 1h 1m 1s")]
    [InlineData(-5, "0s")]
    public void Scales_units_by_threshold(double seconds, string expected) =>
        Assert.Equal(expected, DurationFormatter.FormatDuration(seconds));

    [Fact]
    public void TimeSpan_overload_matches_seconds_overload() =>
        Assert.Equal(DurationFormatter.FormatDuration(127), DurationFormatter.FormatDuration(TimeSpan.FromSeconds(127)));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Rejects_non_finite(double seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DurationFormatter.FormatDuration(seconds));
}
