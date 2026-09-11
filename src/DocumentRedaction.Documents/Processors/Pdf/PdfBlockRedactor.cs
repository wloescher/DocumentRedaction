using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;

namespace DocumentRedaction.Documents.Processors.Pdf;

/// <summary>
/// Detects within one block and turns the result into text runs (what stays) and boxes (what
/// goes). A block's lines are joined with newlines so sentence-widening detectors stop at a line
/// break; a second pass over the same text with spaces instead finds an identifier wrapped
/// across two lines. Both strings have identical offsets, so the two sets merge directly. Before
/// resolving overlaps, every sentence is widened over each wrapped token (and other sentence) it
/// touches, so no half of a token survives on a neighbouring line and a short sentence cannot
/// lose the overlap to a longer token.
/// </summary>
internal sealed class PdfBlockRedactor
{
    private const double BoxPaddingPoints = 0.5;

    private readonly ITextRedactor _redactor;
    private readonly RedactionOptions _options;

    /// <summary>Sentence kinds sit the wrapped-token pass out, or they would widen to the block.</summary>
    private readonly RedactionOptions _tokenOptions;

    public PdfBlockRedactor(ITextRedactor redactor, RedactionOptions options)
    {
        _redactor = redactor;
        _options = options;
        _tokenOptions = options with { ExcludedKinds = options.ExcludedKinds.Union(InformationKinds.SentenceKinds).ToHashSet() };
    }

    public BlockResult Redact(PdfBlock block)
    {
        IReadOnlyList<Detection> detections = Detect(block.Text);
        List<PdfTextRun> runs = [];
        List<PdfRedactionBox> boxes = [];

        int offset = 0;
        foreach (PdfLine line in block.Lines)
        {
            List<(PdfGlyph Glyph, PdfWord Word, int Start)> glyphs = [];
            foreach (PdfWord word in line.Words)
            {
                foreach (PdfGlyph glyph in word.Glyphs)
                {
                    glyphs.Add((glyph, word, offset));
                    offset += glyph.Text.Length;
                }

                offset++; // the space or newline after the word
            }

            bool[] covered = new bool[glyphs.Count];
            foreach (Detection detection in detections)
            {
                List<int> hit = [];
                for (int i = 0; i < glyphs.Count; i++)
                {
                    int start = glyphs[i].Start;
                    int end = start + glyphs[i].Glyph.Text.Length;
                    if (start < detection.End && detection.Start < end)
                    {
                        covered[i] = true;
                        hit.Add(i);
                    }
                }

                if (hit.Count > 0)
                {
                    boxes.Add(Box(hit.Select(i => glyphs[i].Glyph).ToList(), glyphs[hit[0]].Word, detection.Kind));
                }
            }

            runs.AddRange(Runs(line, covered));
        }

        return new BlockResult(runs, boxes, detections);
    }

    private IReadOnlyList<Detection> Detect(string block)
    {
        IReadOnlyList<Detection> byLine = _redactor.Detect(block, _options);
        if (!block.Contains('\n', StringComparison.Ordinal))
        {
            return byLine;
        }

        IReadOnlyList<Detection> acrossLines = _redactor.Detect(block.Replace('\n', ' '), _tokenOptions);
        List<Detection> sentences = byLine.Where(found => InformationKinds.SentenceKinds.Contains(found.Kind)).ToList();
        IEnumerable<Detection> tokens = byLine.Where(found => !InformationKinds.SentenceKinds.Contains(found.Kind)).Concat(acrossLines);

        WidenOverOverlaps(sentences, acrossLines, block);
        return DetectionResolver.Resolve(tokens.Concat(sentences));
    }

    /// <summary>
    /// Grows each sentence to cover every token and sentence it overlaps, repeating until nothing
    /// grows: a widened sentence can touch a new neighbour. Spans only ever grow, so this ends.
    /// </summary>
    private static void WidenOverOverlaps(List<Detection> sentences, IReadOnlyList<Detection> tokens, string block)
    {
        bool grew = true;
        while (grew)
        {
            grew = false;
            Detection[] neighbours = [.. tokens, .. sentences];
            for (int i = 0; i < sentences.Count; i++)
            {
                foreach (Detection other in neighbours)
                {
                    Detection sentence = sentences[i];
                    if (!sentence.Overlaps(other) || (sentence.Start <= other.Start && other.End <= sentence.End))
                    {
                        continue;
                    }

                    int start = Math.Min(sentence.Start, other.Start);
                    int end = Math.Max(sentence.End, other.End);
                    sentences[i] = new Detection(sentence.Kind, start, end - start, block.Substring(start, end - start));
                    grew = true;
                }
            }
        }
    }

    /// <summary>
    /// Each maximal run of uncovered glyphs within a word becomes text, glyph by glyph at the
    /// source positions. A run that ends the word carries a trailing space glyph where its last
    /// glyph's baseline ends: the layout analysis discarded the source's space glyphs, and
    /// without one the text layer of the output would run words together.
    /// </summary>
    private static IEnumerable<PdfTextRun> Runs(PdfLine line, bool[] covered)
    {
        int index = 0;
        foreach (PdfWord word in line.Words)
        {
            List<PdfPlacedGlyph> placed = [];
            PdfGlyph? last = null;
            foreach (PdfGlyph glyph in word.Glyphs)
            {
                if (covered[index++])
                {
                    if (placed.Count > 0)
                    {
                        yield return Run(word, placed);
                        placed = [];
                    }

                    continue;
                }

                placed.Add(new PdfPlacedGlyph(glyph.Text, glyph.BaselineX, glyph.BaselineY));
                last = glyph;
            }

            if (placed.Count > 0)
            {
                placed.Add(new PdfPlacedGlyph(" ", last!.EndX, last.EndY));
                yield return Run(word, placed);
            }
        }
    }

    private static PdfTextRun Run(PdfWord word, List<PdfPlacedGlyph> glyphs) =>
        new(glyphs, word.PointSize, word.IsBold, word.IsItalic, word.FontFamily, word.AngleDegrees);

    /// <summary>
    /// The union of the covered glyph boxes, with a label shrunk so it fits along the reading
    /// direction: the full placeholder when there is room, otherwise just the kind label (a
    /// short value such as an email address cannot hold "[REDACTED-EMAIL]" legibly), otherwise
    /// no label. The label is padded with spaces so the text layer keeps it a word of its own,
    /// and it starts where the first covered glyph started.
    /// </summary>
    private PdfRedactionBox Box(List<PdfGlyph> glyphs, PdfWord word, InformationKind kind)
    {
        double left = double.MaxValue, right = double.MinValue, top = double.MinValue, bottom = double.MaxValue;
        foreach (PdfGlyph glyph in glyphs)
        {
            left = Math.Min(left, glyph.Left);
            right = Math.Max(right, glyph.Right);
            top = Math.Max(top, glyph.Top);
            bottom = Math.Min(bottom, glyph.Bottom);
        }

        left -= BoxPaddingPoints;
        right += BoxPaddingPoints;
        top += BoxPaddingPoints;
        bottom -= BoxPaddingPoints;

        // Room for the label runs along the baseline from the first glyph's start to the last one's end.
        PdfGlyph first = glyphs[0];
        PdfGlyph last = glyphs[^1];
        double extent = Math.Sqrt(Math.Pow(last.EndX - first.BaselineX, 2) + Math.Pow(last.EndY - first.BaselineY, 2)) - 2 * BoxPaddingPoints;
        string label = string.Empty;
        double labelSize = 0;
        foreach (string candidate in new[] { _options.PlaceholderFor(kind), InformationKinds.Get(kind).PlaceholderLabel })
        {
            string padded = $" {candidate} ";
            double fitting = Math.Min(word.PointSize, extent / (PdfLabelStyle.WidthPerPoint * padded.Length));
            if (fitting >= PdfLabelStyle.MinPointSize)
            {
                label = padded;
                labelSize = fitting;
                break;
            }
        }

        return new PdfRedactionBox(left, right, top, bottom, label, labelSize, first.BaselineX, first.BaselineY, word.AngleDegrees);
    }

    internal sealed record BlockResult(IReadOnlyList<PdfTextRun> Runs, IReadOnlyList<PdfRedactionBox> Boxes, IReadOnlyList<Detection> Detections);
}
