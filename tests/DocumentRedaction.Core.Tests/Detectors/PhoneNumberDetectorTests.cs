using DocumentRedaction.Core.Detectors.Builtin;

namespace DocumentRedaction.Core.Tests.Detectors;

public class PhoneNumberDetectorTests
{
    private readonly PhoneNumberDetector _detector = new();

    [Theory]
    [InlineData("call (555) 123-4567 today", "(555) 123-4567")]
    [InlineData("call (555)123-4567 today", "(555)123-4567")]
    [InlineData("call 555-123-4567 today", "555-123-4567")]
    [InlineData("call 555.123.4567 today", "555.123.4567")]
    [InlineData("call 555 123 4567 today", "555 123 4567")]
    [InlineData("call +1 555 123 4567 today", "+1 555 123 4567")]
    [InlineData("call 1-555-123-4567 today", "1-555-123-4567")]
    [InlineData("call 555-123-4567 ext. 89 today", "555-123-4567 ext. 89")]
    [InlineData("call 555-123-4567 x123 today", "555-123-4567 x123")]
    [InlineData("call +44 20 7946 0958 today", "+44 20 7946 0958")]
    [InlineData("call +49 (0)30 1234 5678 today", "+49 (0)30 1234 5678")]
    public void Finds(string text, string expected) => DetectorAssert.Finds(_detector, text, expected);

    [Theory]
    [InlineData("bare 5551234567 digits")]
    [InlineData("order 12345-6789")]
    [InlineData("date 2024-01-02")]
    [InlineData("card 4111 1111 1111 1111")]
    public void Rejects(string text) => DetectorAssert.FindsNothing(_detector, text);
}
