using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>IPv4 addresses with every octet in range.</summary>
public sealed partial class IpAddressDetector : RegexDetector
{
    public override InformationKind Kind => InformationKind.IpAddress;

    protected override Regex Pattern => Ipv4Regex();

    protected override bool IsValid(string value) =>
        value.Split('.').All(octet => int.Parse(octet, System.Globalization.CultureInfo.InvariantCulture) <= 255);

    [GeneratedRegex(@"(?<![\d.])\d{1,3}(?:\.\d{1,3}){3}(?![\d.])")]
    private static partial Regex Ipv4Regex();
}
