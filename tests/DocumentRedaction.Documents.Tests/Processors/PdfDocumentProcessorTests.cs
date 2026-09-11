using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;
using DocumentRedaction.Documents.Processors;
using DocumentRedaction.Tests.Fixtures;
using QuestPDF.Helpers;

namespace DocumentRedaction.Documents.Tests.Processors;

public class PdfDocumentProcessorTests
{
    private readonly PdfDocumentProcessor _processor = new(TextRedactor.CreateDefault());
    private readonly RedactionOptions _options = new();

    [Fact]
    public void Redacts_text_and_reports()
    {
        byte[] input = PdfFixture.Build(["Patient SSN 123-45-6789.", "Contact a@b.co for details."]);
        ProcessedDocument processed = _processor.Redact(input, _options);

        string text = Assert.Single(PdfFixture.ReadPageTexts(processed.Content));
        // Short values only have room for the kind label inside their box.
        Assert.Contains("[REDACTED-SSN]", text, StringComparison.Ordinal);
        Assert.Contains("EMAIL", text, StringComparison.Ordinal);
        Assert.DoesNotContain("123-45-6789", text, StringComparison.Ordinal);
        Assert.DoesNotContain("a@b.co", text, StringComparison.Ordinal);
        Assert.Equal(2, processed.Report.Total);
    }

    [Fact]
    public void Page_count_and_order_are_preserved()
    {
        byte[] input = PdfFixture.Build(["first page a@b.co"], ["second page"], ["third page 123-45-6789"]);
        IReadOnlyList<string> pages = PdfFixture.ReadPageTexts(_processor.Redact(input, _options).Content);

        Assert.Equal(3, pages.Count);
        Assert.Contains("first page EMAIL", pages[0], StringComparison.Ordinal);
        Assert.Contains("second page", pages[1], StringComparison.Ordinal);
        Assert.Contains("third page [REDACTED-SSN]", pages[2], StringComparison.Ordinal);
    }

    [Fact]
    public void Page_size_is_preserved()
    {
        byte[] input = PdfFixture.Build(PageSizes.A4, ["text"]);
        (double width, double height) = Assert.Single(PdfFixture.ReadPageSizes(_processor.Redact(input, _options).Content));
        Assert.Equal(PageSizes.A4.Width, width, 0.5);
        Assert.Equal(PageSizes.A4.Height, height, 0.5);
    }

    [Fact]
    public void Blank_page_among_text_pages_is_kept()
    {
        byte[] input = PdfFixture.Build(["one"], [], ["three"]);
        Assert.Equal(3, PdfFixture.ReadPageTexts(_processor.Redact(input, _options).Content).Count);
    }

    [Fact]
    public void Pdf_without_text_layer_is_rejected()
    {
        EmptyDocumentException ex = Assert.Throws<EmptyDocumentException>(() => _processor.Redact(PdfFixture.BlankPage(), _options));
        Assert.Contains("OCR", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(new byte[] { 1, 2, 3 })]
    [InlineData(new byte[0])]
    public void Corrupt_input_throws_invalid_document(byte[] input) =>
        Assert.Throws<InvalidDocumentException>(() => _processor.Redact(input, _options));

    [Fact]
    public void Category_selection_is_respected()
    {
        byte[] input = PdfFixture.Build(["SSN 123-45-6789 card 4111111111111111"]);
        string text = PdfFixture.ReadPageTexts(_processor.Redact(input, new RedactionOptions { Categories = RedactionCategory.Financial }).Content)[0];
        Assert.Contains("123-45-6789", text, StringComparison.Ordinal);
        Assert.Contains("[REDACTED-CREDIT-CARD]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Confidential_marker_widens_to_its_line_not_the_whole_block()
    {
        byte[] input = PdfFixture.BuildLines("Responsibilities at the firm", "handled confidential client matters", "shipped on time");

        ProcessedDocument processed = _processor.Redact(input, _options);
        string text = Assert.Single(PdfFixture.ReadPageTexts(processed.Content));

        Assert.Contains("[REDACTED-CONFIDENTIAL]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("confidential", text, StringComparison.Ordinal);
        Assert.Contains("Responsibilities at the firm", text, StringComparison.Ordinal);
        Assert.Contains("shipped on time", text, StringComparison.Ordinal);
        Assert.Equal(1, processed.Report.Total);
    }

    [Fact]
    public void Identifier_wrapped_across_lines_is_still_redacted()
    {
        // Also pins the fixture: a wrapped card can only be found when both lines form one block.
        byte[] input = PdfFixture.BuildLines("Card on file 4111 1111 1111", "1111 expires soon");

        ProcessedDocument processed = _processor.Redact(input, _options);
        string text = Assert.Single(PdfFixture.ReadPageTexts(processed.Content));

        Assert.Contains("[REDACTED-CREDIT-CARD]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("4111", text, StringComparison.Ordinal);
        Assert.Contains("expires soon", text, StringComparison.Ordinal);
        Assert.Equal(1, processed.Report.CountsByKind[InformationKind.CreditCardNumber]);
    }

    [Fact]
    public void Token_wrapped_into_a_confidential_line_is_swallowed_whole()
    {
        // The card starts on line 1 and ends on the confidential line; the sentence widens over it.
        byte[] input = PdfFixture.BuildLines("SSN 123-45-6789 and card 4111 1111", "1111 1111 are confidential details", "next item");

        ProcessedDocument processed = _processor.Redact(input, _options);
        string text = Assert.Single(PdfFixture.ReadPageTexts(processed.Content));

        Assert.DoesNotContain("4111", text, StringComparison.Ordinal);
        Assert.DoesNotContain("1111", text, StringComparison.Ordinal);
        Assert.DoesNotContain("123-45-6789", text, StringComparison.Ordinal);
        Assert.Contains("and card", text, StringComparison.Ordinal);
        Assert.Contains("next item", text, StringComparison.Ordinal);
        Assert.Equal(1, processed.Report.CountsByKind[InformationKind.SocialSecurityNumber]);
        Assert.Equal(1, processed.Report.CountsByKind[InformationKind.ConfidentialStatement]);
        Assert.Equal(2, processed.Report.Total);
    }

    [Fact]
    public void Short_confidential_line_is_not_lost_to_a_longer_wrapped_token()
    {
        // The confidential line is shorter than the card that wraps into it; length alone would drop the sentence.
        byte[] input = PdfFixture.BuildLines("SSN 123-45-6789 4111 1111 1111", "1111 Confidential", "next item");

        ProcessedDocument processed = _processor.Redact(input, _options);
        string text = Assert.Single(PdfFixture.ReadPageTexts(processed.Content));

        Assert.DoesNotContain("Confidential", text, StringComparison.Ordinal);
        Assert.DoesNotContain("1111", text, StringComparison.Ordinal);
        Assert.DoesNotContain("123-45-6789", text, StringComparison.Ordinal);
        Assert.Contains("next item", text, StringComparison.Ordinal);
        Assert.Equal(1, processed.Report.CountsByKind[InformationKind.ConfidentialStatement]);
    }

    [Fact]
    public void Token_bridging_two_confidential_lines_merges_them()
    {
        byte[] input = PdfFixture.BuildLines("Confidential 4111 1111", "1111 1111 and this too is confidential stuff with many more words here", "next item");

        ProcessedDocument processed = _processor.Redact(input, _options);
        string text = Assert.Single(PdfFixture.ReadPageTexts(processed.Content));

        Assert.DoesNotContain("Confidential", text, StringComparison.Ordinal);
        Assert.DoesNotContain("1111", text, StringComparison.Ordinal);
        Assert.DoesNotContain("many more", text, StringComparison.Ordinal);
        Assert.Contains("next item", text, StringComparison.Ordinal);
        Assert.Equal(1, processed.Report.Total);
    }

    [Fact]
    public void Token_wrapped_out_of_a_confidential_line_is_swallowed_whole()
    {
        byte[] input = PdfFixture.BuildLines("confidential card 4111 1111", "1111 1111 expires soon", "next item");

        ProcessedDocument processed = _processor.Redact(input, _options);
        string text = Assert.Single(PdfFixture.ReadPageTexts(processed.Content));

        Assert.DoesNotContain("1111", text, StringComparison.Ordinal);
        Assert.Contains("expires soon", text, StringComparison.Ordinal);
        Assert.Contains("next item", text, StringComparison.Ordinal);
        Assert.Equal(1, processed.Report.Total);
    }

    [Fact]
    public void Words_keep_position_size_and_style()
    {
        byte[] input = PdfFixture.BuildStyled(("Plain", false, false), ("Bold", true, false), ("Italic", false, true), ("SSN", false, false), ("123-45-6789", false, false));
        IReadOnlyList<PdfFixture.PdfWord> before = PdfFixture.ReadWords(input).Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();

        IReadOnlyList<PdfFixture.PdfWord> after = PdfFixture.ReadWords(_processor.Redact(input, _options).Content).Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();

        foreach (string text in new[] { "Plain", "Bold", "Italic", "SSN" })
        {
            PdfFixture.PdfWord original = Assert.Single(before, w => w.Text == text);
            PdfFixture.PdfWord kept = Assert.Single(after, w => w.Text == text);
            Assert.Equal(original.Left, kept.Left, 1.0);
            Assert.Equal(original.Baseline, kept.Baseline, 1.0);
            Assert.Equal(original.PointSize, kept.PointSize, 0.1);
            Assert.Equal(original.IsBold, kept.IsBold);
            Assert.Equal(original.IsItalic, kept.IsItalic);
        }

        Assert.DoesNotContain(after, w => w.Text == "123-45-6789");
        Assert.Contains(after, w => w.Text == "[REDACTED-SSN]");
    }

    [Fact]
    public void Dense_page_does_not_overflow_onto_a_second_page()
    {
        string[] lines = Enumerable.Range(1, 45).Select(i => $"Line {i:D2} " + string.Join(' ', Enumerable.Repeat("filler", 12))).ToArray();
        byte[] input = PdfFixture.BuildLines(lines);
        Assert.Single(PdfFixture.ReadPageSizes(input));

        IReadOnlyList<string> pages = PdfFixture.ReadPageTexts(_processor.Redact(input, _options).Content);

        string page = Assert.Single(pages);
        Assert.Contains("Line 01", page, StringComparison.Ordinal);
        Assert.Contains("Line 45", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Bullet_stays_beside_its_item()
    {
        byte[] input = PdfFixture.BuildBulletItem("\u2022", "Item with SSN 123-45-6789 inside");
        PdfFixture.PdfWord bulletBefore = Assert.Single(PdfFixture.ReadWords(input), w => w.Text == "\u2022");

        IReadOnlyList<PdfFixture.PdfWord> after = PdfFixture.ReadWords(_processor.Redact(input, _options).Content);
        PdfFixture.PdfWord bullet = Assert.Single(after, w => w.Text == "\u2022");
        PdfFixture.PdfWord item = Assert.Single(after, w => w.Text == "Item");

        Assert.Equal(bulletBefore.Left, bullet.Left, 1.0);
        Assert.Equal(bullet.Baseline, item.Baseline, 0.5);
        Assert.True(item.Left > bullet.Left);
    }

    [Fact]
    public void Redaction_box_covers_the_original_span()
    {
        byte[] input = PdfFixture.Build(["Patient SSN 123-45-6789 admitted"]);
        PdfFixture.PdfWord ssn = Assert.Single(PdfFixture.ReadWords(input), w => w.Text == "123-45-6789");

        var boxes = PdfFixture.ReadFilledBoxes(_processor.Redact(input, _options).Content);

        var box = Assert.Single(boxes);
        Assert.True(box.Left <= ssn.Left + 0.01 && box.Right >= ssn.Right - 0.01, $"box {box.Left}-{box.Right} must span the value {ssn.Left}-{ssn.Right}");
        Assert.True(box.Bottom <= ssn.Bottom + 0.01 && box.Top >= ssn.Top - 0.01, $"box {box.Bottom}-{box.Top} must span the value {ssn.Bottom}-{ssn.Top}");
    }

    [Fact]
    public void Keyword_survives_when_it_shares_the_word_with_its_value()
    {
        byte[] input = PdfFixture.Build(["Passport:X12345678 noted"]);
        string text = Assert.Single(PdfFixture.ReadPageTexts(_processor.Redact(input, _options).Content));
        Assert.Contains("Passport:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("X12345678", text, StringComparison.Ordinal);
        Assert.Contains("PASSPORT", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void Rotated_page_keeps_size_and_word_position(int rotate)
    {
        byte[] input = PdfFixture.RotatedPage("Hello 123-45-6789 world", rotate);
        (double width, double height) = Assert.Single(PdfFixture.ReadPageSizes(input));
        PdfFixture.PdfWord before = Assert.Single(PdfFixture.ReadWords(input), w => w.Text == "Hello");

        ProcessedDocument processed = _processor.Redact(input, _options);

        (double outWidth, double outHeight) = Assert.Single(PdfFixture.ReadPageSizes(processed.Content));
        Assert.Equal(width, outWidth, 0.5);
        Assert.Equal(height, outHeight, 0.5);
        PdfFixture.PdfWord after = Assert.Single(PdfFixture.ReadWords(processed.Content), w => w.Text == "Hello");
        Assert.Equal(before.Left, after.Left, 1.0);
        Assert.Equal(before.Baseline, after.Baseline, 1.0);
        Assert.Equal(1, processed.Report.Total);
    }

    [Fact]
    public void Cropped_page_keeps_its_crop_size_and_positions_relative_to_the_crop()
    {
        byte[] input = PdfFixture.CroppedPage("Hello 123-45-6789");
        (double width, double height) = Assert.Single(PdfFixture.ReadPageSizes(input));
        Assert.Equal(400, width, 0.5);
        Assert.Equal(600, height, 0.5);
        PdfFixture.PdfWord before = Assert.Single(PdfFixture.ReadWords(input), w => w.Text == "Hello");

        ProcessedDocument processed = _processor.Redact(input, _options);

        (double outWidth, double outHeight) = Assert.Single(PdfFixture.ReadPageSizes(processed.Content));
        Assert.Equal(width, outWidth, 0.5);
        Assert.Equal(height, outHeight, 0.5);
        // PdfPig reports positions relative to the crop box on both sides, so they match as they are.
        PdfFixture.PdfWord after = Assert.Single(PdfFixture.ReadWords(processed.Content), w => w.Text == "Hello");
        Assert.Equal(before.Left, after.Left, 1.0);
        Assert.Equal(before.Baseline, after.Baseline, 1.0);
    }

    [Fact]
    public void Angled_text_keeps_its_slant()
    {
        byte[] input = PdfFixture.AngledTextPage("Hello 123-45-6789");
        PdfFixture.PdfWord before = Assert.Single(PdfFixture.ReadWords(input), w => w.Text == "Hello");

        ProcessedDocument processed = _processor.Redact(input, _options);

        PdfFixture.PdfWord after = Assert.Single(PdfFixture.ReadWords(processed.Content), w => w.Text == "Hello");
        Assert.Equal(before.Left, after.Left, 1.0);
        Assert.Equal(before.Baseline, after.Baseline, 1.0);
        Assert.Equal(before.Top, after.Top, 1.5);
        Assert.Equal(1, processed.Report.Total);
    }

    [Fact]
    public void Unusable_page_geometry_is_reported_as_an_invalid_document()
    {
        // A zero-sized media box parses, but no page can be drawn at that size.
        byte[] input = PdfFixture.RawPage("/MediaBox [0 0 0 0]", "BT /F1 12 Tf 10 10 Td (Hello) Tj ET");
        InvalidDocumentException ex = Assert.Throws<InvalidDocumentException>(() => _processor.Redact(input, _options));
        Assert.Contains("rebuilt", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Honours_cancellation()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => _processor.Redact(PdfFixture.Build(["x"]), _options, cts.Token));
    }

    private static PdfDocumentProcessor WithLimits(Action<DocumentLimits> configure)
    {
        DocumentLimits limits = new();
        configure(limits);
        return new PdfDocumentProcessor(TextRedactor.CreateDefault(), limits);
    }

    [Fact]
    public void Pages_over_the_limit_are_rejected_with_the_counts()
    {
        byte[] input = PdfFixture.Build(["one"], ["two"], ["three"]);
        DocumentLimitExceededException ex = Assert.Throws<DocumentLimitExceededException>(
            () => WithLimits(l => l.MaxPdfPages = 2).Redact(input, _options));

        Assert.Contains("3 pages", ex.Message, StringComparison.Ordinal);
        Assert.Contains("limit is 2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pages_at_the_limit_pass()
    {
        byte[] input = PdfFixture.Build(["one"], ["two"], ["three"]);
        Assert.Equal(3, PdfFixture.ReadPageTexts(WithLimits(l => l.MaxPdfPages = 3).Redact(input, _options).Content).Count);
    }

    [Fact]
    public void Text_over_the_limit_across_pages_is_rejected()
    {
        byte[] input = PdfFixture.Build(["abc"], ["cde"]);
        DocumentLimitExceededException ex = Assert.Throws<DocumentLimitExceededException>(
            () => WithLimits(l => l.MaxTextCharacters = 5).Redact(input, _options));

        Assert.Contains("limit of 5 characters", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_at_the_limit_passes()
    {
        byte[] input = PdfFixture.Build(["ab"], ["cde"]);
        Assert.Equal(2, PdfFixture.ReadPageTexts(WithLimits(l => l.MaxTextCharacters = 5).Redact(input, _options).Content).Count);
    }

    [Fact]
    public void Flate_bomb_over_the_stream_limit_is_rejected()
    {
        byte[] input = PdfFixture.FlateBomb(4 * 1024 * 1024);
        Assert.True(input.Length < 64 * 1024, "the bomb should be small on disk");

        DocumentLimitExceededException ex = Assert.Throws<DocumentLimitExceededException>(
            () => WithLimits(l => l.MaxDecodedBytes = 1024 * 1024).Redact(input, _options));

        Assert.Contains("decoded-size limit of 1,048,576", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Flate_stream_within_the_limit_is_parsed_normally()
    {
        // Whitespace-only content: parses fine, then fails as text-free rather than as over-limit.
        byte[] input = PdfFixture.FlateBomb(100 * 1024);
        Assert.Throws<EmptyDocumentException>(() => WithLimits(l => l.MaxDecodedBytes = 200 * 1024).Redact(input, _options));
    }

    [Fact]
    public void Many_streams_under_the_stream_cap_are_still_bounded_by_the_total()
    {
        byte[] input = PdfFixture.FlateBomb(1024 * 1024, pages: 4);

        DocumentLimitExceededException ex = Assert.Throws<DocumentLimitExceededException>(
            () => WithLimits(l => { l.MaxDecodedBytes = 2 * 1024 * 1024; l.MaxTotalDecodedBytes = 3 * 1024 * 1024; }).Redact(input, _options));

        Assert.Contains("total decoded-size limit of 3,145,728", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bomb_in_the_xref_stream_is_reported_as_over_limit_not_as_empty()
    {
        // PdfPig swallows the failure while reading the xref stream and recovers a zero-page document.
        byte[] input = PdfFixture.XrefStreamBomb(4 * 1024 * 1024);

        Assert.Throws<DocumentLimitExceededException>(() => WithLimits(l => l.MaxDecodedBytes = 1024 * 1024).Redact(input, _options));
    }

    [Fact]
    public void Invalid_limits_are_rejected_at_construction() =>
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentProcessor(TextRedactor.CreateDefault(), new DocumentLimits { MaxPdfPages = 0 }));

    [Fact]
    public void Null_redactor_is_rejected() =>
        Assert.Throws<ArgumentNullException>(() => new PdfDocumentProcessor(null!));
}
