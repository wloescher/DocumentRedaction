namespace DocumentRedaction.Core.Model;

/// <summary>A concrete type of sensitive information that a detector can find.</summary>
public enum InformationKind
{
    SocialSecurityNumber,
    EmailAddress,
    PhoneNumber,
    IpAddress,
    PassportNumber,
    DriversLicenseNumber,
    StreetAddress,
    MedicalRecordNumber,
    HealthPlanId,
    NationalProviderId,
    DeaNumber,
    Date,
    CreditCardNumber,
    BankRoutingNumber,
    Iban,
    SwiftCode,
    AccountNumber,
    ConfidentialStatement,
    CustomTerm,
    DocumentAuthor,
}
