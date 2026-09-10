using System.Text.RegularExpressions;
using DocumentRedaction.Core.Detectors.Validation;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>Nine bare digits that pass the ABA routing checksum and prefix rules.</summary>
public sealed partial class BankRoutingNumberDetector : RegexDetector
{
    public override InformationKind Kind => InformationKind.BankRoutingNumber;

    protected override Regex Pattern => RoutingRegex();

    protected override bool IsValid(string value) => Checksums.IsValidAbaRoutingNumber(value);

    [GeneratedRegex(@"(?<![\d-])\d{9}(?![\d-])")]
    private static partial Regex RoutingRegex();
}
