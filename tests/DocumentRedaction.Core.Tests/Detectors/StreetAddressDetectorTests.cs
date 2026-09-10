using DocumentRedaction.Core.Detectors.Builtin;

namespace DocumentRedaction.Core.Tests.Detectors;

public class StreetAddressDetectorTests
{
    private readonly StreetAddressDetector _detector = new();

    [Theory]
    [InlineData("lives at 123 Main Street, Springfield", "123 Main Street")]
    [InlineData("lives at 123 Main St., Springfield", "123 Main St.")]
    [InlineData("lives at 4500 N Oak Tree Lane Apt 4B", "4500 N Oak Tree Lane Apt 4B")]
    [InlineData("lives at 12 West Elm Blvd, Suite 200.", "12 West Elm Blvd, Suite 200")]
    [InlineData("lives at 1 Martin Luther King Jr Way", "1 Martin Luther King Jr Way")]
    [InlineData("lives at 77A O'Connor Drive", "77A O'Connor Drive")]
    public void Finds(string text, string expected) => DetectorAssert.Finds(_detector, text, expected);

    [Theory]
    [InlineData("4 Elm")]
    [InlineData("in 2 business days")]
    [InlineData("Main Street without a number")]
    [InlineData("123 main street lowercase")]
    [InlineData("50 Ways Streets")]
    public void Rejects(string text) => DetectorAssert.FindsNothing(_detector, text);
}
