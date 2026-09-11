using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace DocumentRedaction.Documents.Processors.Pdf;

/// <summary>
/// Turns a PdfPig page into the <see cref="PdfPage"/> model: words from the nearest-neighbour
/// extractor, grouped into blocks and lines by Docstrum, ordered as a reader would read them.
/// PdfPig already reports positions relative to the crop box and turned for a /Rotate entry, so
/// they are used as they come.
/// </summary>
internal static class PdfLayoutExtractor
{
    private const double MinPointSize = 1;
    private const double MaxPointSize = 400;

    public static PdfPage Extract(Page page)
    {
        List<Word> words = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();
        if (words.Count == 0)
        {
            return new PdfPage(page.Width, page.Height, []);
        }

        IReadOnlyList<TextBlock> blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
        List<PdfBlock> ordered = UnsupervisedReadingOrderDetector.Instance.Get(blocks)
            .OrderBy(block => block.ReadingOrder)
            .Select(block => new PdfBlock(block.TextLines.Select(line => new PdfLine(line.Words.Select(Convert).ToList())).ToList()))
            .ToList();

        return new PdfPage(page.Width, page.Height, ordered);
    }

    private static PdfWord Convert(Word word)
    {
        Letter first = word.Letters[0];

        // A glyph's box covers both its ink and its advance, so a redaction box hides what a
        // viewer would select, not only what the glyph outline touches.
        List<PdfGlyph> glyphs = word.Letters
            .Select(letter => new PdfGlyph(
                letter.Value,
                Math.Min(letter.BoundingBox.Left, Math.Min(letter.StartBaseLine.X, letter.EndBaseLine.X)),
                Math.Max(letter.BoundingBox.Right, Math.Max(letter.StartBaseLine.X, letter.EndBaseLine.X)),
                letter.BoundingBox.Top,
                letter.BoundingBox.Bottom,
                letter.StartBaseLine.X,
                letter.StartBaseLine.Y,
                letter.EndBaseLine.X,
                letter.EndBaseLine.Y))
            .ToList();

        return new PdfWord(
            glyphs,
            Math.Clamp(first.PointSize, MinPointSize, MaxPointSize),
            first.FontDetails.IsBold,
            first.FontDetails.IsItalic,
            PdfFontFamilies.Classify(first.FontName),
            AngleDegrees(glyphs[0]));
    }

    /// <summary>
    /// The baseline's direction as a clockwise screen rotation. PDF space has y up, so a
    /// baseline rising to the right is an anticlockwise turn on screen, hence the sign flip.
    /// </summary>
    public static double AngleDegrees(PdfGlyph glyph)
    {
        double dx = glyph.EndX - glyph.BaselineX;
        double dy = glyph.EndY - glyph.BaselineY;
        if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6)
        {
            return 0;
        }

        double degrees = -Math.Atan2(dy, dx) * 180 / Math.PI;
        degrees = Math.Round(degrees, 3);
        return degrees < 0 ? degrees + 360 : degrees;
    }
}
