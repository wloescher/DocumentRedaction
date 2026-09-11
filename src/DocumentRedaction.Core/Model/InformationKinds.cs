using System.Collections.Frozen;

namespace DocumentRedaction.Core.Model;

/// <summary>Catalog of every supported <see cref="InformationKind"/> and its metadata.</summary>
public static class InformationKinds
{
    private const RedactionCategory PiiAndHipaa = RedactionCategory.Pii | RedactionCategory.Hipaa;

    private static readonly FrozenDictionary<InformationKind, InformationKindInfo> ByKind = new InformationKindInfo[]
    {
        // Checksum-validated kinds rank first so they beat a same-length pattern-only match.
        new(InformationKind.CreditCardNumber, "Credit card number", "CREDIT-CARD", RedactionCategory.Financial, 10),
        new(InformationKind.Iban, "IBAN", "IBAN", RedactionCategory.Financial, 20),
        new(InformationKind.NationalProviderId, "National Provider Identifier (NPI)", "NPI", RedactionCategory.Hipaa, 30),
        new(InformationKind.DeaNumber, "DEA registration number", "DEA", RedactionCategory.Hipaa, 40),
        new(InformationKind.BankRoutingNumber, "Bank routing number (ABA)", "ROUTING-NUMBER", RedactionCategory.Financial, 50),
        new(InformationKind.SocialSecurityNumber, "Social Security number", "SSN", PiiAndHipaa, 60),
        new(InformationKind.CustomTerm, "Custom term", "CUSTOM", RedactionCategory.None, 70),
        new(InformationKind.EmailAddress, "Email address", "EMAIL", PiiAndHipaa, 80),
        new(InformationKind.SwiftCode, "SWIFT/BIC code", "SWIFT", RedactionCategory.Financial, 90),
        new(InformationKind.MedicalRecordNumber, "Medical record number", "MRN", RedactionCategory.Hipaa, 100),
        new(InformationKind.HealthPlanId, "Health plan / beneficiary ID", "HEALTH-PLAN-ID", RedactionCategory.Hipaa, 110),
        new(InformationKind.PassportNumber, "Passport number", "PASSPORT", PiiAndHipaa, 120),
        new(InformationKind.DriversLicenseNumber, "Driver's license number", "DRIVERS-LICENSE", PiiAndHipaa, 130),
        new(InformationKind.AccountNumber, "Account number", "ACCOUNT-NUMBER", RedactionCategory.Financial, 140),
        new(InformationKind.PhoneNumber, "Phone number", "PHONE", PiiAndHipaa, 150),
        new(InformationKind.IpAddress, "IP address", "IP", PiiAndHipaa, 160),
        new(InformationKind.StreetAddress, "Street address", "ADDRESS", PiiAndHipaa, 170),
        new(InformationKind.Date, "Date", "DATE", RedactionCategory.Hipaa, 180),
        new(InformationKind.ConfidentialStatement, "Confidential statement", "CONFIDENTIAL", RedactionCategory.Confidential, 190, WidensToSentence: true),
        new(InformationKind.DocumentAuthor, "Document author", "AUTHOR", PiiAndHipaa, 200, MetadataOnly: true),
    }.ToFrozenDictionary(info => info.Kind);

    /// <summary>All kinds, ordered by priority.</summary>
    public static IReadOnlyList<InformationKindInfo> All { get; } =
        ByKind.Values.OrderBy(info => info.Priority).ToArray();

    /// <summary>Kinds whose detections widen to a sentence; see <see cref="InformationKindInfo.WidensToSentence"/>.</summary>
    public static IReadOnlySet<InformationKind> SentenceKinds { get; } =
        All.Where(info => info.WidensToSentence).Select(info => info.Kind).ToHashSet();

    /// <summary>Kinds found in document metadata rather than text; see <see cref="InformationKindInfo.MetadataOnly"/>.</summary>
    public static IReadOnlySet<InformationKind> MetadataKinds { get; } =
        All.Where(info => info.MetadataOnly).Select(info => info.Kind).ToHashSet();

    public static InformationKindInfo Get(InformationKind kind) =>
        ByKind.TryGetValue(kind, out InformationKindInfo? info)
            ? info
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown information kind.");

    /// <summary>Kinds that belong to at least one of the given categories.</summary>
    public static IEnumerable<InformationKindInfo> ForCategories(RedactionCategory categories) =>
        All.Where(info => (info.Categories & categories) != RedactionCategory.None);
}
