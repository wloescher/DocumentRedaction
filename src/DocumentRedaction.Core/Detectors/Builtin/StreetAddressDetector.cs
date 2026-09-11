using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>
/// US-style street addresses: a house number, one to four capitalised words, a street suffix
/// and an optional unit. Pattern-based, so unusual formats are missed.
/// </summary>
public sealed partial class StreetAddressDetector : RegexDetector
{
    /// <summary>Street suffixes, full and abbreviated; shared with <see cref="PersonNameDetector"/> as words that end a name.</summary>
    internal const string SuffixAlternation = "Street|St|Avenue|Ave|Road|Rd|Boulevard|Blvd|Lane|Ln|Drive|Dr|Court|Ct|Way|Place|Pl|Circle|Cir|Terrace|Ter|Parkway|Pkwy|Highway|Hwy|Trail|Trl|Square|Sq";

    public override InformationKind Kind => InformationKind.StreetAddress;

    protected override Regex Pattern => AddressRegex();

    [GeneratedRegex(
        @"\b\d{1,6}[A-Za-z]?\s+(?:(?:[NSEW]|North|South|East|West)\.?\s+)?(?:[A-Z][A-Za-z']+\s+){1,4}" +
        @"(?:" + SuffixAlternation + @")\.?" +
        @"(?:\s*,?\s*(?:Apt|Apartment|Suite|Ste|Unit|Floor|Fl|#)\.?\s*[A-Za-z0-9-]+)?" +
        @"(?![A-Za-z])",
        RegexOptions.ExplicitCapture)]
    private static partial Regex AddressRegex();
}
