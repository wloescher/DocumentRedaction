using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>
/// Matches SSNs written with hyphen or space separators, or nine bare digits when introduced
/// by "SSN" / "Social Security". Excludes area numbers 000, 666 and 900-999, group 00 and
/// serial 0000, which the SSA never issues.
/// </summary>
public sealed partial class SocialSecurityNumberDetector : RegexDetector
{
    public override InformationKind Kind => InformationKind.SocialSecurityNumber;

    protected override Regex Pattern => SsnRegex();

    [GeneratedRegex(
        @"(?<![\d-])(?:" +
        @"(?<value>(?!000|666|9\d\d)\d{3}[- ](?!00)\d{2}[- ](?!0000)\d{4})" +
        @"|\b(?i:SSN|social\s+security(?:\s+(?:number|no\.?|#))?)\s*[:#]?\s*(?<value>(?!000|666|9\d\d)\d{3}(?!00)\d{2}(?!0000)\d{4})" +
        @")(?![\d-])",
        RegexOptions.ExplicitCapture)]
    private static partial Regex SsnRegex();
}
