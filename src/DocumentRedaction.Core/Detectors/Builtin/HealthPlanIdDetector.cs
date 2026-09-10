using System.Text.RegularExpressions;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors.Builtin;

/// <summary>
/// Health plan, member, subscriber, policy and group identifiers introduced by a keyword, plus
/// Medicare Beneficiary Identifiers (MBI), whose 11-character structure is distinctive enough
/// to match without a keyword.
/// </summary>
public sealed partial class HealthPlanIdDetector : RegexDetector
{
    public override InformationKind Kind => InformationKind.HealthPlanId;

    protected override Regex Pattern => HealthPlanRegex();

    [GeneratedRegex(
        @"(?:" +
        @"\b(?i:member|subscriber|policy|group|health\s+plan|beneficiary|insurance|medicaid|medicare)\s+(?i:id|number|no|num|#)\.?\s*[:#]?\s*(?<value>(?=[A-Za-z0-9-]*\d)[A-Za-z0-9](?:[A-Za-z0-9-]{3,18})[A-Za-z0-9])\b" +
        @"|\b(?<value>[1-9][AC-HJ-KM-NP-RT-Y][AC-HJ-KM-NP-RT-Y0-9]\d[AC-HJ-KM-NP-RT-Y][AC-HJ-KM-NP-RT-Y0-9]\d[AC-HJ-KM-NP-RT-Y]{2}\d{2})\b" +
        @")",
        RegexOptions.ExplicitCapture)]
    private static partial Regex HealthPlanRegex();
}
