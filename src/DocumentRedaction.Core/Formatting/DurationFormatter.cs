using System.Globalization;

namespace DocumentRedaction.Core.Formatting;

/// <summary>
/// The single place that turns a duration into display text, used for both estimated time
/// remaining and total elapsed time. Scales from seconds to days automatically.
/// </summary>
public static class DurationFormatter
{
    public static string FormatDuration(TimeSpan duration) => FormatDuration(duration.TotalSeconds);

    public static string FormatDuration(double totalSeconds)
    {
        if (double.IsNaN(totalSeconds) || double.IsInfinity(totalSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(totalSeconds), totalSeconds, "Duration must be a finite number of seconds.");
        }

        long seconds = (long)Math.Round(Math.Max(0, totalSeconds));
        long days = seconds / 86_400;
        long hours = seconds % 86_400 / 3_600;
        long minutes = seconds % 3_600 / 60;
        long remainder = seconds % 60;

        CultureInfo invariant = CultureInfo.InvariantCulture;
        return seconds switch
        {
            < 60 => string.Create(invariant, $"{remainder}s"),
            < 3_600 => string.Create(invariant, $"{minutes}m {remainder}s"),
            < 86_400 => string.Create(invariant, $"{hours}h {minutes}m {remainder}s"),
            _ => string.Create(invariant, $"{days}d {hours}h {minutes}m {remainder}s"),
        };
    }
}
