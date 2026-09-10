using System.Text.RegularExpressions;
using DocumentRedaction.Core.Detectors.Validation;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>
/// Payment card numbers of 13-19 digits, either contiguous or in the conventional 4-4-4-4 /
/// 4-6-5 groupings with a consistent separator, validated with Luhn.
/// </summary>
public sealed partial class CreditCardNumberDetector : RegexDetector
{
    public override InformationKind Kind => InformationKind.CreditCardNumber;

    protected override Regex Pattern => CardRegex();

    protected override bool IsValid(string value)
    {
        string digits = new(value.Where(char.IsAsciiDigit).ToArray());
        return digits.Length is >= 13 and <= 19 && Checksums.IsValidLuhn(digits);
    }

    [GeneratedRegex(
        @"(?<![\d-])(?:" +
        @"\d{4}(?<sep>[ -])\d{4}\k<sep>\d{4}\k<sep>\d{4}(?:\k<sep>\d{1,3})?" +
        @"|\d{4}(?<sep>[ -])\d{6}\k<sep>\d{5}" +
        @"|\d{13,19}" +
        @")(?![\d-])",
        RegexOptions.ExplicitCapture)]
    private static partial Regex CardRegex();
}
