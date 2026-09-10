using System.Text.RegularExpressions;
using DocumentRedaction.Core.Detectors.Validation;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>Ten bare digits that pass the NPI Luhn check (with the 80840 prefix).</summary>
public sealed partial class NationalProviderIdDetector : RegexDetector
{
    public override InformationKind Kind => InformationKind.NationalProviderId;

    protected override Regex Pattern => NpiRegex();

    protected override bool IsValid(string value) => Checksums.IsValidNpi(value);

    [GeneratedRegex(@"(?<![\d-])\d{10}(?![\d-])")]
    private static partial Regex NpiRegex();
}
