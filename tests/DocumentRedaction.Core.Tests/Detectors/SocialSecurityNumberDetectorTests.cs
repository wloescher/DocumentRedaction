using DocumentRedaction.Core.Detectors.Builtin;

namespace DocumentRedaction.Core.Tests.Detectors;

public class SocialSecurityNumberDetectorTests
{
    private readonly SocialSecurityNumberDetector _detector = new();

    [Theory]
    [InlineData("SSN 123-45-6789 on file", "123-45-6789")]
    [InlineData("SSN 123 45 6789 on file", "123 45 6789")]
    [InlineData("SSN: 123456789", "123456789")]
    [InlineData("Social Security Number: 123456789", "123456789")]
    [InlineData("social security # 123456789", "123456789")]
    [InlineData("ssn#123456789", "123456789")]
    public void Finds_valid_forms(string text, string expected) => DetectorAssert.Finds(_detector, text, expected);

    [Theory]
    [InlineData("bare 123456789 digits")]
    [InlineData("000-45-6789")]
    [InlineData("666-45-6789")]
    [InlineData("900-45-6789")]
    [InlineData("123-00-6789")]
    [InlineData("123-45-0000")]
    [InlineData("1123-45-6789")]
    [InlineData("123-45-67890")]
    [InlineData("123-45-6789-1")]
    [InlineData("phone 555-123-4567")]
    public void Rejects_invalid_or_ambiguous(string text) => DetectorAssert.FindsNothing(_detector, text);

    [Fact]
    public void Finds_multiple() =>
        DetectorAssert.Finds(_detector, "A 123-45-6789 and B 876-65-4321.", "123-45-6789", "876-65-4321");
}
