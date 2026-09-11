using DocumentRedaction.Documents.Processors.Pdf;

namespace DocumentRedaction.Documents.Tests.Processors.Pdf;

public class PdfFontFamiliesTests
{
    [Theory]
    [InlineData("ABCDEF+TimesNewRomanPSMT", PdfFontFamilies.Serif)]
    [InlineData("Georgia-Bold", PdfFontFamilies.Serif)]
    [InlineData("Garamond", PdfFontFamilies.Serif)]
    [InlineData("CourierNewPSMT", PdfFontFamilies.Monospace)]
    [InlineData("XYZ+DejaVuSansMono", PdfFontFamilies.Monospace)]
    [InlineData("Helvetica", PdfFontFamilies.SansSerif)]
    [InlineData("CAAAAA+Carlito-Regular", PdfFontFamilies.SansSerif)]
    [InlineData("NotoSerif", PdfFontFamilies.Serif)]
    [InlineData("OpenSans-Bold", PdfFontFamilies.SansSerif)]
    [InlineData("", PdfFontFamilies.SansSerif)]
    [InlineData(null, PdfFontFamilies.SansSerif)]
    public void Classifies_by_name_hints(string? fontName, string expected) =>
        Assert.Equal(expected, PdfFontFamilies.Classify(fontName));
}
