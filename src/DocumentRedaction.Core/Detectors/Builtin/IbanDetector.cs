using System.Collections.Frozen;
using System.Text.RegularExpressions;
using DocumentRedaction.Core.Detectors.Validation;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>
/// IBANs, with or without the conventional four-character spacing, validated with mod-97.
/// The country code fixes the IBAN's length, which stops a following upper-case token
/// ("... 7654 32 USD") from being swallowed into the candidate.
/// </summary>
public sealed partial class IbanDetector : IDetector
{
    /// <summary>Total IBAN length per country, from the ISO 13616 registry.</summary>
    private static readonly FrozenDictionary<string, int> LengthByCountry = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["AD"] = 24, ["AE"] = 23, ["AL"] = 28, ["AT"] = 20, ["AZ"] = 28, ["BA"] = 20, ["BE"] = 16, ["BG"] = 22,
        ["BH"] = 22, ["BR"] = 29, ["BY"] = 28, ["CH"] = 21, ["CR"] = 22, ["CY"] = 28, ["CZ"] = 24, ["DE"] = 22,
        ["DK"] = 18, ["DO"] = 28, ["EE"] = 20, ["EG"] = 29, ["ES"] = 24, ["FI"] = 18, ["FO"] = 18, ["FR"] = 27,
        ["GB"] = 22, ["GE"] = 22, ["GI"] = 23, ["GL"] = 18, ["GR"] = 27, ["GT"] = 28, ["HR"] = 21, ["HU"] = 28,
        ["IE"] = 22, ["IL"] = 23, ["IQ"] = 23, ["IS"] = 26, ["IT"] = 27, ["JO"] = 30, ["KW"] = 30, ["KZ"] = 20,
        ["LB"] = 28, ["LC"] = 32, ["LI"] = 21, ["LT"] = 20, ["LU"] = 20, ["LV"] = 21, ["LY"] = 25, ["MC"] = 27,
        ["MD"] = 24, ["ME"] = 22, ["MK"] = 19, ["MR"] = 27, ["MT"] = 31, ["MU"] = 30, ["NL"] = 18, ["NO"] = 15,
        ["PK"] = 24, ["PL"] = 28, ["PS"] = 29, ["PT"] = 25, ["QA"] = 29, ["RO"] = 24, ["RS"] = 22, ["SA"] = 24,
        ["SC"] = 31, ["SD"] = 18, ["SE"] = 24, ["SI"] = 19, ["SK"] = 24, ["SM"] = 27, ["ST"] = 25, ["SV"] = 28,
        ["TL"] = 23, ["TN"] = 24, ["TR"] = 26, ["UA"] = 29, ["VA"] = 22, ["VG"] = 24, ["XK"] = 20,
    }.ToFrozenDictionary();

    public InformationKind Kind => InformationKind.Iban;

    public IEnumerable<Detection> Detect(string text, RedactionOptions options)
    {
        foreach (Match match in IbanRegex().Matches(text))
        {
            if (!LengthByCountry.TryGetValue(match.Value[..2], out int expectedLength))
            {
                continue;
            }

            // Walk forward until the country's quota of alphanumerics is consumed.
            int end = match.Index;
            int consumed = 0;
            while (consumed < expectedLength && end < match.Index + match.Length)
            {
                if (char.IsAsciiLetterOrDigit(text[end]))
                {
                    consumed++;
                }

                end++;
            }

            bool endsAtBoundary = end == text.Length || !char.IsAsciiLetterOrDigit(text[end]);
            if (consumed < expectedLength || !endsAtBoundary)
            {
                continue;
            }

            string span = text[match.Index..end];
            if (Checksums.IsValidIban(span.Replace(" ", string.Empty, StringComparison.Ordinal)))
            {
                yield return new Detection(Kind, match.Index, span.Length, span);
            }
        }
    }

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}(?: ?[A-Z0-9]{4}){2,7}(?: ?[A-Z0-9]{1,4})?\b")]
    private static partial Regex IbanRegex();
}
