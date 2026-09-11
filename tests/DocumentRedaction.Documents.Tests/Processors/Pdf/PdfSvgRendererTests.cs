using System.Xml.Linq;
using DocumentRedaction.Documents.Processors.Pdf;

namespace DocumentRedaction.Documents.Tests.Processors.Pdf;

public class PdfSvgRendererTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    [Fact]
    public void Flips_y_and_escapes_text()
    {
        PdfRedactedPage page = new(612, 792, [new PdfTextRun([new PdfPlacedGlyph("a<b>&c", 40, 700)], 12, IsBold: true, IsItalic: true, PdfFontFamilies.Serif)], []);

        XElement svg = PdfSvgRenderer.PageSvg(page);
        XElement text = Assert.Single(svg.Elements(Svg + "text"));

        Assert.Equal("a<b>&c", text.Value);
        Assert.Contains("a&lt;b&gt;&amp;c", svg.ToString(SaveOptions.DisableFormatting), StringComparison.Ordinal);
        Assert.Equal("40", text.Attribute("x")!.Value);
        Assert.Equal("92", text.Attribute("y")!.Value);
        Assert.Equal("12", text.Attribute("font-size")!.Value);
        Assert.Equal("serif", text.Attribute("font-family")!.Value);
        Assert.Equal("bold", text.Attribute("font-weight")!.Value);
        Assert.Equal("italic", text.Attribute("font-style")!.Value);
        Assert.Equal("0 0 612 792", svg.Attribute("viewBox")!.Value);
    }

    [Fact]
    public void Plain_runs_carry_no_style_attributes()
    {
        PdfRedactedPage page = new(100, 100, [new PdfTextRun([new PdfPlacedGlyph("x", 1, 2), new PdfPlacedGlyph(" ", 7, 2)], 3, false, false, PdfFontFamilies.SansSerif)], []);
        List<XElement> texts = PdfSvgRenderer.PageSvg(page).Elements(Svg + "text").ToList();
        Assert.Equal(["x", " "], texts.Select(t => t.Value));
        Assert.Equal(["1", "7"], texts.Select(t => t.Attribute("x")!.Value));
        XElement text = texts[0];
        Assert.Null(text.Attribute("font-weight"));
        Assert.Null(text.Attribute("font-style"));
        Assert.Equal("preserve", text.Attribute(XNamespace.Xml + "space")!.Value);
    }

    [Fact]
    public void Box_becomes_a_black_rect_with_a_white_label_on_the_baseline()
    {
        PdfRedactedPage page = new(612, 792, [], [new PdfRedactionBox(100, 200, 720, 708, " [REDACTED-SSN] ", 8, 101, 710)]);

        XElement svg = PdfSvgRenderer.PageSvg(page);
        XElement rect = Assert.Single(svg.Elements(Svg + "rect"));
        XElement label = Assert.Single(svg.Elements(Svg + "text"));

        Assert.Equal("100", rect.Attribute("x")!.Value);
        Assert.Equal("72", rect.Attribute("y")!.Value);
        Assert.Equal("100", rect.Attribute("width")!.Value);
        Assert.Equal("12", rect.Attribute("height")!.Value);
        Assert.Equal("black", rect.Attribute("fill")!.Value);
        Assert.Equal(" [REDACTED-SSN] ", label.Value);
        Assert.Equal("white", label.Attribute("fill")!.Value);
        Assert.Equal("101", label.Attribute("x")!.Value);
        Assert.Null(label.Attribute("transform"));
        Assert.Equal("82", label.Attribute("y")!.Value);
        Assert.Equal("8", label.Attribute("font-size")!.Value);
    }

    [Fact]
    public void Narrow_box_is_drawn_without_a_label()
    {
        PdfRedactedPage page = new(612, 792, [], [new PdfRedactionBox(100, 104, 720, 708, " [REDACTED-SSN] ", 0, 101, 710)]);
        XElement svg = PdfSvgRenderer.PageSvg(page);
        Assert.Single(svg.Elements(Svg + "rect"));
        Assert.Empty(svg.Elements(Svg + "text"));
    }

    [Theory]
    [InlineData(90, "rotate(90 700 280)")]
    [InlineData(180, "rotate(180 700 280)")]
    [InlineData(330, "rotate(330 700 280)")]
    public void Turned_text_is_rotated_about_its_baseline_start(double degrees, string expected)
    {
        PdfRedactedPage page = new(792, 612, [new PdfTextRun([new PdfPlacedGlyph("x", 700, 332)], 12, false, false, PdfFontFamilies.SansSerif, degrees)], []);
        XElement text = Assert.Single(PdfSvgRenderer.PageSvg(page).Elements(Svg + "text"));
        Assert.Equal(expected, text.Attribute("transform")!.Value);
    }

    [Fact]
    public void Numbers_use_invariant_dots_and_three_decimals() =>
        Assert.Equal("1.235", Assert.Single(PdfSvgRenderer.PageSvg(new PdfRedactedPage(1.2345, 1, [], [])).Attributes("width")).Value);

    [Fact]
    public void Control_characters_are_replaced_so_the_svg_stays_valid_xml()
    {
        Assert.Equal("ab", PdfSvgRenderer.XmlSafe("ab"));
        Assert.Equal("a\uFFFDb\uFFFD", PdfSvgRenderer.XmlSafe("a\u0001b\u000C"));
        Assert.Equal("tab\tok", PdfSvgRenderer.XmlSafe("tab\tok"));

        PdfRedactedPage page = new(100, 100, [new PdfTextRun([new PdfPlacedGlyph("\u0007", 1, 2)], 3, false, false, PdfFontFamilies.SansSerif)], []);
        XElement text = Assert.Single(PdfSvgRenderer.PageSvg(page).Elements(Svg + "text"));
        Assert.Equal("\uFFFD", text.Value);
        Assert.NotNull(text.ToString());
    }

    [Fact]
    public void Glyphs_and_boxes_with_non_finite_geometry_are_skipped()
    {
        PdfRedactedPage page = new(
            100,
            100,
            [new PdfTextRun([new PdfPlacedGlyph("a", double.NaN, 2), new PdfPlacedGlyph("b", 5, double.PositiveInfinity), new PdfPlacedGlyph("c", 5, 6)], 3, false, false, PdfFontFamilies.SansSerif)],
            [new PdfRedactionBox(double.NaN, 10, 10, 0, " X ", 4, 0, 0), new PdfRedactionBox(0, 10, 10, 0, " X ", 4, 1, 5)]);

        XElement svg = PdfSvgRenderer.PageSvg(page);

        Assert.Equal(["c", " X "], svg.Elements(Svg + "text").Select(t => t.Value));
        Assert.Single(svg.Elements(Svg + "rect"));
    }
}
