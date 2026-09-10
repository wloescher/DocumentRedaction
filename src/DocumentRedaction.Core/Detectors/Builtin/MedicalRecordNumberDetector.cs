using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>Medical record numbers introduced by "MRN" or "medical record number".</summary>
public sealed partial class MedicalRecordNumberDetector : RegexDetector
{
    public override InformationKind Kind => InformationKind.MedicalRecordNumber;

    protected override Regex Pattern => MrnRegex();

    [GeneratedRegex(
        @"\b(?i:MRN|medical\s+record(?:\s+(?:no|number|num|#))?|patient\s+(?:id|number|no))\.?\s*[:#]?\s*(?<value>(?=[A-Za-z0-9-]*\d)[A-Za-z0-9](?:[A-Za-z0-9-]{2,13})[A-Za-z0-9])\b",
        RegexOptions.ExplicitCapture)]
    private static partial Regex MrnRegex();
}
