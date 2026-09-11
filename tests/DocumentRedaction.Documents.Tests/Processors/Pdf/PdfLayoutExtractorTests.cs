using DocumentRedaction.Documents.Processors.Pdf;

namespace DocumentRedaction.Documents.Tests.Processors.Pdf;

public class PdfLayoutExtractorTests
{
    private static PdfGlyph Glyph(double endX, double endY) => new("a", 0, 0, 0, 0, 100, 700, endX, endY);

    [Theory]
    [InlineData(110, 700, 0)]
    [InlineData(100, 690, 90)]
    [InlineData(90, 700, 180)]
    [InlineData(100, 710, 270)]
    [InlineData(108.66, 705, 330)]
    public void Angle_is_the_clockwise_screen_rotation_of_the_baseline(double endX, double endY, double expected) =>
        Assert.Equal(expected, PdfLayoutExtractor.AngleDegrees(Glyph(endX, endY)), 0.01);

    [Fact]
    public void Zero_length_baseline_is_horizontal() =>
        Assert.Equal(0, PdfLayoutExtractor.AngleDegrees(Glyph(100, 700)));
}
