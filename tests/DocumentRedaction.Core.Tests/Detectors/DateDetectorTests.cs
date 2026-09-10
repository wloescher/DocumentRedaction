using DocumentRedaction.Core.Detectors.Builtin;

namespace DocumentRedaction.Core.Tests.Detectors;

public class DateDetectorTests
{
    private readonly DateDetector _detector = new();

    [Theory]
    [InlineData("DOB 01/02/2024 ok", "01/02/2024")]
    [InlineData("DOB 1/2/24 ok", "1/2/24")]
    [InlineData("DOB 31-12-1999 ok", "31-12-1999")]
    [InlineData("DOB 2024-01-02 ok", "2024-01-02")]
    [InlineData("DOB Jan 2, 2024 ok", "Jan 2, 2024")]
    [InlineData("DOB January 2nd 2024 ok", "January 2nd 2024")]
    [InlineData("DOB Sept. 30, 1985 ok", "Sept. 30, 1985")]
    [InlineData("DOB 2 January 2024 ok", "2 January 2024")]
    [InlineData("DOB 2nd jan, 2024 ok", "2nd jan, 2024")]
    public void Finds(string text, string expected) => DetectorAssert.Finds(_detector, text, expected);

    [Theory]
    [InlineData("in 2024")]
    [InlineData("13/13/2024")]
    [InlineData("00/10/2024")]
    [InlineData("2024-13-01")]
    [InlineData("2024-00-10")]
    [InlineData("Jan 32, 2024")]
    [InlineData("01/02-2024")] // mixed separators
    [InlineData("phone 555-123-4567")]
    [InlineData("SSN 123-45-6789")]
    [InlineData("ratio 1/2")]
    public void Rejects(string text) => DetectorAssert.FindsNothing(_detector, text);
}
