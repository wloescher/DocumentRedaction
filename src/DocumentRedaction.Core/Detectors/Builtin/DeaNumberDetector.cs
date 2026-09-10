using System.Text.RegularExpressions;
using DocumentRedaction.Core.Detectors.Validation;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>DEA registration numbers: registrant-type letter, initial letter or 9, seven checksummed digits.</summary>
public sealed partial class DeaNumberDetector : RegexDetector
{
    public override InformationKind Kind => InformationKind.DeaNumber;

    protected override Regex Pattern => DeaRegex();

    protected override bool IsValid(string value) => Checksums.IsValidDeaNumber(value);

    [GeneratedRegex(@"\b[ABCDEFGHJKLMPRSTUX][A-Z9]\d{7}\b")]
    private static partial Regex DeaRegex();
}
