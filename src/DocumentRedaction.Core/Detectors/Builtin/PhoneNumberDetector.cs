using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>
/// North American numbers with at least one separator or parenthesised area code, plus
/// international numbers in "+CC ..." form. Ten bare digits are deliberately not matched
/// because they are indistinguishable from other identifiers.
/// </summary>
public sealed partial class PhoneNumberDetector : RegexDetector
{
    public override InformationKind Kind => InformationKind.PhoneNumber;

    protected override Regex Pattern => PhoneRegex();

    [GeneratedRegex(
        @"(?<![\d+-])(?:" +
        @"(?:\+?1[\s.-]?)?(?:\(\d{3}\)\s?|\d{3}[\s.-])\d{3}[\s.-]\d{4}" +
        @"|\+(?!1\b)\d{1,3}[\s.-]?(?:\(?\d{1,4}\)?[\s.-]?){2,4}\d{2,4}" +
        @")(?![\d-])" +
        @"(?:\s*(?i:ext|x|extension)\.?\s*\d{1,5})?",
        RegexOptions.ExplicitCapture)]
    private static partial Regex PhoneRegex();
}
