namespace DocumentRedaction.Documents.Processors.Pdf;

/// <summary>One page's text as PdfPig found it, in PDF points with the origin at the bottom left.</summary>
internal sealed record PdfPage(double Width, double Height, IReadOnlyList<PdfBlock> Blocks);

/// <summary>A paragraph-like group of lines in reading order; detection runs over one block at a time.</summary>
internal sealed record PdfBlock(IReadOnlyList<PdfLine> Lines)
{
    /// <summary>The block as the detectors see it: words joined by spaces, lines by newlines.</summary>
    public string Text { get; } = string.Join('\n', Lines.Select(line => string.Join(' ', line.Words.Select(word => word.Text))));
}

internal sealed record PdfLine(IReadOnlyList<PdfWord> Words);

/// <summary>
/// A word with its glyphs, so a detection that covers part of a word can be boxed exactly.
/// <paramref name="AngleDegrees"/> is the reading direction as a clockwise screen rotation:
/// 0 for ordinary text, 90 on a page displayed turned a quarter clockwise, any value for text
/// set along a slanted baseline.
/// </summary>
internal sealed record PdfWord(IReadOnlyList<PdfGlyph> Glyphs, double PointSize, bool IsBold, bool IsItalic, string FontFamily, double AngleDegrees = 0)
{
    public string Text => string.Concat(Glyphs.Select(glyph => glyph.Text));
}

/// <summary>
/// One glyph: its text (a ligature may carry two characters), its axis-aligned box covering ink
/// and advance, and the start and end of its baseline, which together give its direction.
/// </summary>
internal sealed record PdfGlyph(string Text, double Left, double Right, double Top, double Bottom, double BaselineX, double BaselineY, double EndX, double EndY);

/// <summary>
/// Text to draw, one glyph at a time at its own baseline start, produced from the glyphs a
/// detection did not cover. Placing every glyph where the source had it keeps the original
/// spacing even though the substitute font has different widths.
/// </summary>
internal sealed record PdfTextRun(IReadOnlyList<PdfPlacedGlyph> Glyphs, double PointSize, bool IsBold, bool IsItalic, string FontFamily, double AngleDegrees = 0)
{
    public string Text => string.Concat(Glyphs.Select(glyph => glyph.Text));
}

internal sealed record PdfPlacedGlyph(string Text, double X, double BaselineY);

/// <summary>
/// A filled box covering a redacted span on one line, with the placeholder drawn inside it on
/// the baseline the covered glyphs sat on. A <see cref="LabelPointSize"/> of zero means the box
/// is too narrow for a legible label and is drawn without one.
/// </summary>
internal sealed record PdfRedactionBox(double Left, double Right, double Top, double Bottom, string Label, double LabelPointSize, double LabelX, double LabelBaselineY, double AngleDegrees = 0);

/// <summary>Everything the renderer needs for one output page.</summary>
internal sealed record PdfRedactedPage(double Width, double Height, IReadOnlyList<PdfTextRun> Runs, IReadOnlyList<PdfRedactionBox> Boxes);
