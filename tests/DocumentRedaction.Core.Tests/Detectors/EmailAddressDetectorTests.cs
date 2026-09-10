using DocumentRedaction.Core.Detectors.Builtin;

namespace DocumentRedaction.Core.Tests.Detectors;

public class EmailAddressDetectorTests
{
    private readonly EmailAddressDetector _detector = new();

    [Theory]
    [InlineData("mail john.doe@example.com now", "john.doe@example.com")]
    [InlineData("<first+tag@sub.example.co.uk>", "first+tag@sub.example.co.uk")]
    [InlineData("Contact: A_B%C@Example.ORG.", "A_B%C@Example.ORG")]
    public void Finds(string text, string expected) => DetectorAssert.Finds(_detector, text, expected);

    [Theory]
    [InlineData("not an email @example.com")]
    [InlineData("user@localhost")]
    [InlineData("user@.com")]
    [InlineData("user@example.c")]
    public void Rejects(string text) => DetectorAssert.FindsNothing(_detector, text);
}
