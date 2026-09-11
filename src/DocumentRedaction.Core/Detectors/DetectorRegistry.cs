using DocumentRedaction.Core.Detectors.Builtin;
using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Detectors;

/// <summary>The built-in detectors. Supply your own set to <see cref="Redaction.TextRedactor"/> to extend or replace them.</summary>
public static class DetectorRegistry
{
    public static IReadOnlyList<IDetector> CreateDefaultDetectors() =>
    [
        new SocialSecurityNumberDetector(),
        new EmailAddressDetector(),
        new PhoneNumberDetector(),
        new IpAddressDetector(),
        new PassportNumberDetector(),
        new DriversLicenseNumberDetector(),
        new StreetAddressDetector(),
        new PersonNameDetector(),
        new MedicalRecordNumberDetector(),
        new HealthPlanIdDetector(),
        new NationalProviderIdDetector(),
        new DeaNumberDetector(),
        new DateDetector(),
        new CreditCardNumberDetector(),
        new BankRoutingNumberDetector(),
        new IbanDetector(),
        new SwiftCodeDetector(),
        new AccountNumberDetector(),
        new ConfidentialStatementDetector(),
        new CustomTermDetector(),
    ];

    /// <summary>
    /// Throws when a text kind in the catalog has no detector, or a detector has no catalog entry.
    /// Metadata-only kinds are redacted by document processors and need no detector.
    /// </summary>
    public static void EnsureComplete(IReadOnlyList<IDetector> detectors)
    {
        HashSet<InformationKind> covered = detectors.Select(detector => detector.Kind).ToHashSet();
        InformationKind[] missing = InformationKinds.All
            .Select(info => info.Kind)
            .Where(kind => !InformationKinds.MetadataKinds.Contains(kind) && !covered.Contains(kind))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"No detector registered for: {string.Join(", ", missing)}.");
        }

        foreach (IDetector detector in detectors)
        {
            _ = InformationKinds.Get(detector.Kind);
        }
    }
}
