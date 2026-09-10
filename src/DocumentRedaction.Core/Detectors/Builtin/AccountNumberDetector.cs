using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>
/// Bank or card account numbers introduced by "account", "acct", "a/c", "routing" or "ABA".
/// The value is either hyphenated groups, space-separated four-digit groups, or one contiguous
/// run, so two adjacent numbers separated by a space are not merged into one identifier.
/// </summary>
public sealed partial class AccountNumberDetector : RegexDetector
{
    public override InformationKind Kind => InformationKind.AccountNumber;

    protected override Regex Pattern => AccountRegex();

    protected override bool IsValid(string value) => value.Count(char.IsAsciiDigit) is >= 6 and <= 24;

    [GeneratedRegex(
        @"\b(?i:account|acct|a/c|routing|ABA)(?:\s+(?i:no|number|num|#))?\.?\s*[:#]?\s*" +
        @"(?<value>(?:\d{4} ){1,5}\d{2,4}|\d+(?:-\d+){1,5}|\d{6,24})(?![\d-])",
        RegexOptions.ExplicitCapture)]
    private static partial Regex AccountRegex();
}
