namespace DocumentRedaction.Core.Model;

/// <summary>
/// High-level categories a user can choose to redact. A single <see cref="InformationKind"/>
/// may belong to several categories (an SSN is both PII and a HIPAA identifier).
/// </summary>
[Flags]
public enum RedactionCategory
{
    None = 0,

    /// <summary>Personally identifiable information.</summary>
    Pii = 1,

    /// <summary>HIPAA protected health information identifiers.</summary>
    Hipaa = 2,

    /// <summary>Payment and banking identifiers.</summary>
    Financial = 4,

    /// <summary>Confidentiality markers and user-supplied terms.</summary>
    Confidential = 8,

    All = Pii | Hipaa | Financial | Confidential,
}
