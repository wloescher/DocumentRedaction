using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;
using DocumentRedaction.Documents.Processors.Pdf;

namespace DocumentRedaction.Documents.Tests.Processors.Pdf;

public class PdfBlockRedactorTests
{
    private const double Size = 10;

    /// <summary>Lays words out left to right on one baseline, 6 points per character plus a 4 point gap.</summary>
    private static PdfLine Line(double baseline, params string[] words)
    {
        List<PdfWord> built = [];
        double x = 50;
        foreach (string word in words)
        {
            List<PdfGlyph> glyphs = [];
            foreach (char c in word)
            {
                glyphs.Add(new PdfGlyph(c.ToString(), x, x + 6, baseline + 8, baseline - 2, x, baseline, x + 6, baseline));
                x += 6;
            }

            built.Add(new PdfWord(glyphs, Size, IsBold: false, IsItalic: false, PdfFontFamilies.SansSerif));
            x += 4;
        }

        return new PdfLine(built);
    }

    private static PdfBlockRedactor Redactor(RedactionOptions? options = null) => new(TextRedactor.CreateDefault(), options ?? new RedactionOptions());

    [Fact]
    public void Block_text_joins_words_with_spaces_and_lines_with_newlines() =>
        Assert.Equal("a b\nc", new PdfBlock([Line(700, "a", "b"), Line(688, "c")]).Text);

    [Fact]
    public void Untouched_words_become_runs_with_a_trailing_space()
    {
        PdfBlockRedactor.BlockResult result = Redactor().Redact(new PdfBlock([Line(700, "plain", "words")]));

        Assert.Empty(result.Boxes);
        Assert.Empty(result.Detections);
        Assert.Collection(result.Runs,
            run =>
            {
                Assert.Equal("plain ", run.Text);
                Assert.Equal([50, 56, 62, 68, 74, 80], run.Glyphs.Select(glyph => glyph.X));
                Assert.All(run.Glyphs, glyph => Assert.Equal(700, glyph.BaselineY));
                Assert.Equal(Size, run.PointSize);
            },
            run => { Assert.Equal("words ", run.Text); Assert.Equal(50 + 5 * 6 + 4, run.Glyphs[0].X); });
    }

    [Fact]
    public void Detected_word_becomes_a_box_and_no_run()
    {
        PdfBlockRedactor.BlockResult result = Redactor().Redact(new PdfBlock([Line(700, "SSN", "123-45-6789", "end")]));

        PdfRedactionBox box = Assert.Single(result.Boxes);
        Assert.Equal(" [REDACTED-SSN] ", box.Label);
        double left = 50 + 3 * 6 + 4;
        Assert.Equal(left - 0.5, box.Left, 3);
        Assert.Equal(left + 11 * 6 + 0.5, box.Right, 3);
        Assert.Equal(708.5, box.Top, 3);
        Assert.Equal(697.5, box.Bottom, 3);
        Assert.Equal(left, box.LabelX);
        Assert.Equal(700, box.LabelBaselineY);
        Assert.Equal(0, box.AngleDegrees);
        Assert.Equal(["SSN ", "end "], result.Runs.Select(run => run.Text));
    }

    [Fact]
    public void Value_inside_a_word_keeps_the_rest_of_the_word_as_runs()
    {
        // The keyword-anchored passport detector reports only the value, so "Passport:" survives.
        PdfBlockRedactor.BlockResult result = Redactor().Redact(new PdfBlock([Line(700, "Passport:X12345678", "noted")]));

        PdfRedactionBox box = Assert.Single(result.Boxes);
        Assert.Equal(" PASSPORT ", box.Label);
        Assert.Equal(50 + 9 * 6 - 0.5, box.Left, 3);
        Assert.Collection(result.Runs,
            run => Assert.Equal("Passport:", run.Text),
            run => Assert.Equal("noted ", run.Text));
    }

    [Fact]
    public void Span_across_words_is_one_box_per_line()
    {
        PdfBlockRedactor.BlockResult result = Redactor().Redact(new PdfBlock([Line(700, "card", "4111", "1111", "1111"), Line(688, "1111", "ok")]));

        Assert.Equal(2, result.Boxes.Count);
        Assert.Equal(700, result.Boxes[0].LabelBaselineY);
        Assert.Equal(688, result.Boxes[1].LabelBaselineY);
        Assert.Equal(["card ", "ok "], result.Runs.Select(run => run.Text));
        Assert.Equal(InformationKind.CreditCardNumber, Assert.Single(result.Detections).Kind);
    }

    [Fact]
    public void Label_shrinks_to_fit_and_is_dropped_when_it_cannot()
    {
        PdfBlockRedactor.BlockResult wide = Redactor().Redact(new PdfBlock([Line(700, "SSN", "123-45-6789")]));
        PdfRedactionBox wideBox = Assert.Single(wide.Boxes);
        Assert.Equal(" [REDACTED-SSN] ", wideBox.Label);
        Assert.True(wideBox.LabelPointSize >= PdfLabelStyle.MinPointSize && wideBox.LabelPointSize <= Size);

        // Six characters (36 points) cannot hold the full placeholder legibly, so only the kind label is used.
        PdfBlockRedactor.BlockResult medium = Redactor().Redact(new PdfBlock([Line(700, "a@b.co")]));
        PdfRedactionBox mediumBox = Assert.Single(medium.Boxes);
        Assert.Equal(" EMAIL ", mediumBox.Label);
        Assert.True(mediumBox.LabelPointSize >= PdfLabelStyle.MinPointSize);

        // A single-character value: the box is 6 points wide, far too narrow for any label.
        RedactionOptions terms = new() { Categories = RedactionCategory.None, CustomTerms = ["x"] };
        PdfBlockRedactor.BlockResult narrow = Redactor(terms).Redact(new PdfBlock([Line(700, "x")]));
        PdfRedactionBox narrowBox = Assert.Single(narrow.Boxes);
        Assert.Equal(0, narrowBox.LabelPointSize);
        Assert.Equal(string.Empty, narrowBox.Label);
    }

    [Fact]
    public void Turned_words_keep_their_angle_and_fit_the_label_along_the_baseline()
    {
        // Glyphs stacked downwards as on a page turned a quarter clockwise: each baseline runs 6 points down.
        List<PdfGlyph> glyphs = [];
        double y = 700;
        foreach (char c in "123-45-6789")
        {
            glyphs.Add(new PdfGlyph(c.ToString(), 100, 110, y, y - 6, 100, y, 100, y - 6));
            y -= 6;
        }

        PdfWord word = new(glyphs, Size, false, false, PdfFontFamilies.SansSerif, 90);
        PdfBlockRedactor.BlockResult result = Redactor().Redact(new PdfBlock([new PdfLine([word])]));

        PdfRedactionBox box = Assert.Single(result.Boxes);
        Assert.Equal(90, box.AngleDegrees);
        Assert.Equal(65 / (PdfLabelStyle.WidthPerPoint * " [REDACTED-SSN] ".Length), box.LabelPointSize, 3);
        Assert.Empty(result.Runs);
    }

    [Fact]
    public void Trailing_space_sits_where_the_last_baseline_ends()
    {
        PdfGlyph slanted = new("a", 100, 106, 708, 700, 100, 700, 105.2, 703);
        PdfWord word = new([slanted], Size, false, false, PdfFontFamilies.SansSerif, 330);

        PdfTextRun run = Assert.Single(Redactor().Redact(new PdfBlock([new PdfLine([word])])).Runs);

        Assert.Equal(330, run.AngleDegrees);
        PdfPlacedGlyph space = run.Glyphs[^1];
        Assert.Equal(" ", space.Text);
        Assert.Equal(105.2, space.X);
        Assert.Equal(703, space.BaselineY);
    }

    [Fact]
    public void Multi_character_glyph_is_covered_when_any_of_its_characters_are()
    {
        // The passport value starts at "X", but "X" shares a glyph with the colon, so the whole glyph is boxed.
        List<PdfGlyph> glyphs = [];
        double x = 50;
        foreach (string text in new[] { "P", "a", "s", "s", "p", "o", "r", "t", ":X", "1", "2", "3", "4", "5", "6", "7", "8" })
        {
            double width = 6 * text.Length;
            glyphs.Add(new PdfGlyph(text, x, x + width, 708, 698, x, 700, x + width, 700));
            x += width;
        }

        PdfBlock block = new([new PdfLine([new PdfWord(glyphs, Size, false, false, PdfFontFamilies.SansSerif)])]);

        PdfBlockRedactor.BlockResult result = Redactor().Redact(block);

        Assert.Equal(50 + 8 * 6 - 0.5, Assert.Single(result.Boxes).Left, 3);
        Assert.Equal("Passport", Assert.Single(result.Runs).Text);
    }
}
