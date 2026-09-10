using DocumentRedaction.Core.Detectors;
using DocumentRedaction.Core.Detectors.Builtin;

namespace DocumentRedaction.Core.Tests.Detectors;

/// <summary>Detectors that require an introducing keyword and report only the identifier that follows it.</summary>
public class KeywordAnchoredDetectorTests
{
    [Theory]
    [InlineData("Passport: X12345678", "X12345678")]
    [InlineData("passport no. 123456789", "123456789")]
    [InlineData("Passport Number 123456", "123456")]
    [InlineData("PASSPORT#AB123456", "AB123456")]
    public void Passport_finds(string text, string expected) => DetectorAssert.Finds(new PassportNumberDetector(), text, expected);

    [Theory]
    [InlineData("X12345678 without keyword")]
    [InlineData("passport 12345")]
    [InlineData("passports are 123456789")]
    [InlineData("passport applicant")]
    public void Passport_rejects(string text) => DetectorAssert.FindsNothing(new PassportNumberDetector(), text);

    [Theory]
    [InlineData("Driver's License: D1234567", "D1234567")]
    [InlineData("drivers licence no. S123-456-789", "S123-456-789")]
    [InlineData("DL# 12345678", "12345678")]
    [InlineData("License Number: A12345", "A12345")]
    public void DriversLicense_finds(string text, string expected) => DetectorAssert.Finds(new DriversLicenseNumberDetector(), text, expected);

    [Theory]
    [InlineData("handled 12345678")]
    [InlineData("D1234567 without keyword")]
    [InlineData("DL: 12")]
    [InlineData("software license agreement")]
    public void DriversLicense_rejects(string text) => DetectorAssert.FindsNothing(new DriversLicenseNumberDetector(), text);

    [Theory]
    [InlineData("MRN: 00123456", "00123456")]
    [InlineData("Medical Record Number 98-7654", "98-7654")]
    [InlineData("medical record # ABC123", "ABC123")]
    [InlineData("Patient ID: P0001", "P0001")]
    public void Mrn_finds(string text, string expected) => DetectorAssert.Finds(new MedicalRecordNumberDetector(), text, expected);

    [Theory]
    [InlineData("00123456 without keyword")]
    [InlineData("MRN: 12")]
    [InlineData("medical record review")]
    public void Mrn_rejects(string text) => DetectorAssert.FindsNothing(new MedicalRecordNumberDetector(), text);

    [Theory]
    [InlineData("Member ID: XYZ123456", "XYZ123456")]
    [InlineData("Subscriber Number 000-111-222", "000-111-222")]
    [InlineData("Group No. GRP99999", "GRP99999")]
    [InlineData("Health Plan ID H1234567", "H1234567")]
    [InlineData("Medicare number 1EG4TE5MK73", "1EG4TE5MK73")]
    [InlineData("MBI 1EG4TE5MK73 without keyword", "1EG4TE5MK73")]
    public void HealthPlan_finds(string text, string expected) => DetectorAssert.Finds(new HealthPlanIdDetector(), text, expected);

    [Theory]
    [InlineData("XYZ123456 without keyword")]
    [InlineData("0EG4TE5MK73")] // MBI position 1 cannot be 0
    [InlineData("1SG4TE5MK73")] // S is excluded from MBI letters
    [InlineData("group number policy")]
    public void HealthPlan_rejects(string text) => DetectorAssert.FindsNothing(new HealthPlanIdDetector(), text);

    [Theory]
    [InlineData("SWIFT: DEUTDEFF", "DEUTDEFF")]
    [InlineData("BIC code DEUTDEFF500", "DEUTDEFF500")]
    [InlineData("swift no. CHASUS33", "CHASUS33")]
    public void Swift_finds(string text, string expected) => DetectorAssert.Finds(new SwiftCodeDetector(), text, expected);

    [Theory]
    [InlineData("STANDARD PRACTICE")]
    [InlineData("DEUTDEFF without keyword")]
    [InlineData("SWIFT: deutdeff")]
    public void Swift_rejects(string text) => DetectorAssert.FindsNothing(new SwiftCodeDetector(), text);

    [Theory]
    [InlineData("Account No: 123456789012", "123456789012")]
    [InlineData("acct # 1234-5678-90", "1234-5678-90")]
    [InlineData("A/C 12345678", "12345678")]
    [InlineData("Routing: 021000021", "021000021")]
    [InlineData("ABA 021000021", "021000021")]
    [InlineData("Account 12345678 555-123-4567", "12345678")]
    [InlineData("Account 4111 1111 1111 1111 ok", "4111 1111 1111 1111")]
    public void Account_finds(string text, string expected) => DetectorAssert.Finds(new AccountNumberDetector(), text, expected);

    [Theory]
    [InlineData("account 1234")]
    [InlineData("accountable 123456789")]
    [InlineData("123456789012 without keyword")]
    [InlineData("account 12-34")] // fewer than six digits
    public void Account_rejects(string text) => DetectorAssert.FindsNothing(new AccountNumberDetector(), text);

    [Fact]
    public void Reported_span_excludes_keyword()
    {
        IDetector detector = new PassportNumberDetector();
        const string text = "Passport: X12345678.";
        var detection = Assert.Single(DetectorAssert.Run(detector, text));
        Assert.Equal(text.IndexOf("X1", StringComparison.Ordinal), detection.Start);
        Assert.Equal(9, detection.Length);
    }
}
