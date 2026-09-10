using System.Globalization;
using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>
/// Calendar dates in numeric (1/2/2024, 01-02-24, 2024-01-02) and textual (Jan 2, 2024;
/// 2 January 2024) forms. Bare years are not matched because HIPAA permits them.
/// </summary>
public sealed partial class DateDetector : RegexDetector
{
    private static readonly string[] MonthNames =
    [
        "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec",
    ];

    public override InformationKind Kind => InformationKind.Date;

    protected override Regex Pattern => DateRegex();

    public override IEnumerable<Detection> Detect(string text, RedactionOptions options)
    {
        foreach (Match match in Pattern.Matches(text))
        {
            if (IsPlausibleDate(match))
            {
                yield return new Detection(Kind, match.Index, match.Length, match.Value);
            }
        }
    }

    private static bool IsPlausibleDate(Match match)
    {
        if (match.Groups["iso"].Success)
        {
            return IsValidMonthDay(Int(match, "isoM"), Int(match, "isoD"));
        }

        if (match.Groups["num"].Success)
        {
            int first = Int(match, "numA");
            int second = Int(match, "numB");
            // Accept either month/day or day/month order.
            return IsValidMonthDay(first, second) || IsValidMonthDay(second, first);
        }

        string month = match.Groups["month"].Value[..3].ToLowerInvariant();
        int day = Int(match, "day");
        return IsValidMonthDay(Array.IndexOf(MonthNames, month) + 1, day);
    }

    private static bool IsValidMonthDay(int month, int day) => month is >= 1 and <= 12 && day is >= 1 and <= 31;

    private static int Int(Match match, string group) =>
        int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);

    [GeneratedRegex(
        @"(?<![\d/-])(?:" +
        @"(?<iso>(?:19|20)\d{2}-(?<isoM>\d{2})-(?<isoD>\d{2}))" +
        @"|(?<num>(?<numA>\d{1,2})(?<sep>[/-])(?<numB>\d{1,2})\k<sep>(?:(?:19|20)\d{2}|\d{2}))" +
        @"|(?<mdy>(?<month>Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|June?|July?|Aug(?:ust)?|Sep(?:t(?:ember)?)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\.?\s+(?<day>\d{1,2})(?:st|nd|rd|th)?,?\s+(?:19|20)\d{2})" +
        @"|(?<dmy>(?<day>\d{1,2})(?:st|nd|rd|th)?\s+(?<month>Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|June?|July?|Aug(?:ust)?|Sep(?:t(?:ember)?)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\.?,?\s+(?:19|20)\d{2})" +
        @")(?![\d/-])",
        RegexOptions.IgnoreCase)]
    private static partial Regex DateRegex();
}
