using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>Passport numbers introduced by the word "passport"; bare values are too ambiguous.</summary>
public sealed partial class PassportNumberDetector : RegexDetector
{
    public override InformationKind Kind => InformationKind.PassportNumber;

    protected override Regex Pattern => PassportRegex();

    [GeneratedRegex(
        @"\b(?i:passport)(?:\s+(?i:no|number|num|#))?\.?\s*[:#]?\s*(?<value>(?=[A-Za-z0-9]*\d)[A-Za-z0-9]{6,9})\b",
        RegexOptions.ExplicitCapture)]
    private static partial Regex PassportRegex();
}
