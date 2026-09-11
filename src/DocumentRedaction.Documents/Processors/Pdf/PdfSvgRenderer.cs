using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace DocumentRedaction.Documents.Processors.Pdf;

/// <summary>
/// Draws each page as an SVG of positioned text and filled boxes at the original page size, so
/// line breaks, columns, bullets and page count survive. Fonts are substituted by generic
/// family; images and vector graphics from the source are not carried over. Every glyph is its
/// own text element: a per-character x list on one element would be smaller, but the text
/// layer PdfPig reads back from it comes out with the letters reordered.
/// </summary>
internal static class PdfSvgRenderer
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    public static byte[] Render(IReadOnlyList<PdfRedactedPage> pages) =>
        Document.Create(container =>
        {
            foreach (PdfRedactedPage page in pages)
            {
                container.Page(descriptor =>
                {
                    descriptor.Size((float)page.Width, (float)page.Height, Unit.Point);
                    descriptor.Margin(0);
                    descriptor.Content().Svg(PageSvg(page).ToString(SaveOptions.DisableFormatting)).FitArea();
                });
            }
        }).GeneratePdf();

    /// <summary>SVG has its origin at the top left, so every y is flipped against the page height.</summary>
    public static XElement PageSvg(PdfRedactedPage page)
    {
        XElement svg = new(
            Svg + "svg",
            new XAttribute("width", Number(page.Width)),
            new XAttribute("height", Number(page.Height)),
            new XAttribute("viewBox", $"0 0 {Number(page.Width)} {Number(page.Height)}"));

        foreach (PdfTextRun run in page.Runs)
        {
            foreach (PdfPlacedGlyph glyph in run.Glyphs)
            {
                if (!AllFinite(glyph.X, glyph.BaselineY, run.PointSize, run.AngleDegrees))
                {
                    continue; // a broken font can yield NaN metrics; there is nowhere to draw such a glyph
                }

                svg.Add(Text(glyph.Text, glyph.X, page.Height - glyph.BaselineY, run.PointSize, run.FontFamily, run.IsBold, run.IsItalic, "black", run.AngleDegrees));
            }
        }

        foreach (PdfRedactionBox box in page.Boxes)
        {
            if (!AllFinite(box.Left, box.Right, box.Top, box.Bottom, box.LabelX, box.LabelBaselineY, box.LabelPointSize, box.AngleDegrees))
            {
                continue;
            }

            svg.Add(new XElement(
                Svg + "rect",
                new XAttribute("x", Number(box.Left)),
                new XAttribute("y", Number(page.Height - box.Top)),
                new XAttribute("width", Number(box.Right - box.Left)),
                new XAttribute("height", Number(box.Top - box.Bottom)),
                new XAttribute("fill", "black")));

            if (box.LabelPointSize > 0)
            {
                svg.Add(Text(box.Label, box.LabelX, page.Height - box.LabelBaselineY, box.LabelPointSize, PdfLabelStyle.FontFamily, isBold: false, isItalic: false, "white", box.AngleDegrees));
            }
        }

        return svg;
    }

    private static XElement Text(string text, double x, double y, double pointSize, string fontFamily, bool isBold, bool isItalic, string fill, double angleDegrees)
    {
        XElement element = new(
            Svg + "text",
            new XAttribute("x", Number(x)),
            new XAttribute("y", Number(y)),
            new XAttribute("font-size", Number(pointSize)),
            new XAttribute("font-family", fontFamily),
            new XAttribute("fill", fill),
            new XAttribute(XNamespace.Xml + "space", "preserve"),
            XmlSafe(text));

        if (isBold)
        {
            element.Add(new XAttribute("font-weight", "bold"));
        }

        if (isItalic)
        {
            element.Add(new XAttribute("font-style", "italic"));
        }

        if (Math.Abs(angleDegrees) > 0.001)
        {
            // SVG rotates clockwise about the given point; the baseline start is that point.
            element.Add(new XAttribute("transform", $"rotate({Number(angleDegrees)} {Number(x)} {Number(y)})"));
        }

        return element;
    }

    /// <summary>XML 1.0 cannot carry most control characters; odd encodings in PDFs produce them, so they become U+FFFD.</summary>
    public static string XmlSafe(string text)
    {
        if (text.All(XmlConvert.IsXmlChar))
        {
            return text;
        }

        return string.Concat(text.Select(c => XmlConvert.IsXmlChar(c) ? c : '\uFFFD'));
    }

    private static bool AllFinite(params double[] values) => values.All(double.IsFinite);

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
