using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>
/// Driver's license numbers introduced by "driver's license", "DL" or "license no". The value must
/// contain a digit so that "license agreement" is not treated as an identifier.
/// </summary>
public sealed partial class DriversLicenseNumberDetector : RegexDetector
{
    public override InformationKind Kind => InformationKind.DriversLicenseNumber;

    protected override Regex Pattern => LicenseRegex();

    [GeneratedRegex(
        @"\b(?i:driver'?s?\s+licen[cs]e|DL|licen[cs]e)(?:\s+(?i:no|number|num|#))?\.?\s*[:#]?\s*(?<value>(?=[A-Za-z0-9-]*\d)[A-Za-z0-9](?:[A-Za-z0-9-]{3,13})[A-Za-z0-9])\b",
        RegexOptions.ExplicitCapture)]
    private static partial Regex LicenseRegex();
}
