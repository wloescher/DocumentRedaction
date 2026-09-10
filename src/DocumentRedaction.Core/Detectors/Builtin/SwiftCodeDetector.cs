using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>
/// SWIFT/BIC codes introduced by "SWIFT" or "BIC". Bare 8/11-character codes look like
/// ordinary capitalised words ("STANDARD"), so a keyword is required.
/// </summary>
public sealed partial class SwiftCodeDetector : RegexDetector
{
    public override InformationKind Kind => InformationKind.SwiftCode;

    protected override Regex Pattern => SwiftRegex();

    [GeneratedRegex(
        @"\b(?i:SWIFT|BIC)(?:\s+(?i:code|no|number))?\.?\s*[:#]?\s*(?<value>[A-Z]{6}[A-Z0-9]{2}(?:[A-Z0-9]{3})?)\b",
        RegexOptions.ExplicitCapture)]
    private static partial Regex SwiftRegex();
}
