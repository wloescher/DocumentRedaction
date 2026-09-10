using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

public sealed partial class EmailAddressDetector : RegexDetector
{
    public override InformationKind Kind => InformationKind.EmailAddress;

    protected override Regex Pattern => EmailRegex();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)*\.[A-Za-z]{2,}\b")]
    private static partial Regex EmailRegex();
}
