using DocumentRedaction.Core.Detectors.Builtin;

namespace DocumentRedaction.Core.Tests.Detectors;

public class IpAddressDetectorTests
{
    private readonly IpAddressDetector _detector = new();

    [Theory]
    [InlineData("host 192.168.1.1 up", "192.168.1.1")]
    [InlineData("host 10.0.0.255 up", "10.0.0.255")]
    [InlineData("(0.0.0.0)", "0.0.0.0")]
    public void Finds(string text, string expected) => DetectorAssert.Finds(_detector, text, expected);

    [Theory]
    [InlineData("256.1.1.1")]
    [InlineData("1.2.3")]
    [InlineData("1.2.3.4.5")]
    [InlineData("version 1.2.3.4567")]
    public void Rejects(string text) => DetectorAssert.FindsNothing(_detector, text);
}
