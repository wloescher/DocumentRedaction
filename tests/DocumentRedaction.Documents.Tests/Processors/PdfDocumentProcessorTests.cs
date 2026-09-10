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
        Assert.Contains("[REDACTED-SSN]", text, StringComparison.Ordinal);
        Assert.Contains("[REDACTED-EMAIL]", text, StringComparison.Ordinal);
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
        Assert.Contains("first page [REDACTED-EMAIL]", pages[0], StringComparison.Ordinal);
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
    public void Honours_cancellation()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => _processor.Redact(PdfFixture.Build(["x"]), _options, cts.Token));
    }
}
