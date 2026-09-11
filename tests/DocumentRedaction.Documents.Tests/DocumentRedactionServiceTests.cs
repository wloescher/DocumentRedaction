using System.Text;
using DocumentRedaction.Core.Model;
using DocumentRedaction.Documents.Processors;
using DocumentRedaction.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentRedaction.Documents.Tests;

public class DocumentRedactionServiceTests
{
    private static IDocumentRedactionService Create() =>
        new ServiceCollection().AddDocumentRedaction().BuildServiceProvider().GetRequiredService<IDocumentRedactionService>();

    [Fact]
    public async Task Redacts_text_stream_and_names_output()
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes("SSN 123-45-6789"));
        RedactedDocument result = await Create().RedactAsync(input, "notes.txt", "text/plain", new RedactionOptions());

        Assert.Equal("notes-redacted.txt", result.FileName);
        Assert.Equal("text/plain", result.ContentType);
        Assert.Equal("SSN [REDACTED-SSN]", Encoding.UTF8.GetString(result.Content.Span));
        Assert.Equal(1, result.Report.Total);
    }

    [Fact]
    public async Task Redacts_word_stream()
    {
        using MemoryStream input = new(WordFixture.WithParagraphs("mail a@b.co"));
        RedactedDocument result = await Create().RedactAsync(input, "Report.docx", null, new RedactionOptions());

        Assert.Equal("Report-redacted.docx", result.FileName);
        Assert.Equal(["mail [REDACTED-EMAIL]"], WordFixture.ReadParagraphs(result.Content));
    }

    [Fact]
    public async Task Redacts_pdf_stream()
    {
        using MemoryStream input = new(PdfFixture.Build(["mail a@b.co"]));
        RedactedDocument result = await Create().RedactAsync(input, "scan.pdf", "application/pdf", new RedactionOptions());

        Assert.Equal("scan-redacted.pdf", result.FileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Contains("EMAIL", PdfFixture.ReadPageTexts(result.Content)[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Output_extension_follows_processor_not_input()
    {
        using MemoryStream input = new(Encoding.UTF8.GetBytes("x"));
        RedactedDocument result = await Create().RedactAsync(input, "notes.md", null, new RedactionOptions());
        Assert.Equal("notes-redacted.txt", result.FileName);
    }

    [Theory]
    [InlineData("report.docx", ".docx", "report-redacted.docx")]
    [InlineData("archive.tar.gz", ".txt", "archive.tar-redacted.txt")]
    [InlineData(".hidden", ".txt", "document-redacted.txt")]
    [InlineData("", ".pdf", "document-redacted.pdf")]
    public void Output_file_name_rules(string original, string extension, string expected) =>
        Assert.Equal(expected, DocumentRedactionService.OutputFileName(original, extension));

    [Fact]
    public async Task Unsupported_type_throws_before_reading_the_stream()
    {
        using MemoryStream input = new([1, 2, 3]);
        await Assert.ThrowsAsync<UnsupportedDocumentFormatException>(() => Create().RedactAsync(input, "a.xlsx", null, new RedactionOptions()));
        Assert.Equal(0, input.Position);
    }

    [Fact]
    public async Task Corrupt_document_surfaces_invalid_document()
    {
        using MemoryStream input = new([1, 2, 3]);
        await Assert.ThrowsAsync<InvalidDocumentException>(() => Create().RedactAsync(input, "a.docx", null, new RedactionOptions()));
    }

    [Fact]
    public async Task Cancellation_is_propagated()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        using MemoryStream input = new(Encoding.UTF8.GetBytes("x"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().RedactAsync(input, "a.txt", null, new RedactionOptions(), cts.Token));
    }

    [Fact]
    public void Registration_resolves_three_processors()
    {
        IDocumentProcessorResolver resolver = new ServiceCollection().AddDocumentRedaction().BuildServiceProvider().GetRequiredService<IDocumentProcessorResolver>();
        Assert.Equal([DocumentFormat.PlainText, DocumentFormat.Word, DocumentFormat.Pdf], resolver.Processors.Select(p => p.Format));
    }

    [Fact]
    public void Default_limits_are_registered_when_none_given()
    {
        ServiceProvider provider = new ServiceCollection().AddDocumentRedaction().BuildServiceProvider();
        Assert.Equal(DocumentLimits.DefaultMaxPdfPages, provider.GetRequiredService<DocumentLimits>().MaxPdfPages);
    }

    [Fact]
    public void Factory_limits_reach_the_processors()
    {
        DocumentLimits limits = new() { MaxPdfPages = 1 };
        ServiceProvider provider = new ServiceCollection().AddDocumentRedaction(_ => limits).BuildServiceProvider();
        Assert.Same(limits, provider.GetRequiredService<DocumentLimits>());
        PdfDocumentProcessor pdf = Assert.Single(provider.GetServices<IDocumentProcessor>().OfType<PdfDocumentProcessor>());
        Assert.Throws<DocumentLimitExceededException>(() => pdf.Redact(PdfFixture.Build(["a"], ["b"]), new RedactionOptions()));
    }

    [Fact]
    public void Factory_license_is_applied_when_the_pdf_processor_is_built()
    {
        int calls = 0;
        ServiceProvider provider = new ServiceCollection()
            .AddDocumentRedaction(pdfLicense: _ => { calls++; return QuestPdfLicense.Professional; })
            .BuildServiceProvider();

        Assert.Single(provider.GetServices<IDocumentProcessor>().OfType<PdfDocumentProcessor>());
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Invalid_factory_limits_fail_when_a_processor_is_resolved()
    {
        ServiceProvider provider = new ServiceCollection().AddDocumentRedaction(_ => new DocumentLimits { MaxDecodedBytes = 0 }).BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IDocumentProcessorResolver>());
    }

    [Fact]
    public async Task Warnings_pass_through_from_the_processor()
    {
        byte[] docx = WordFixture.Build(main =>
        {
            main.Document!.Body!.Append(WordFixture.Paragraph("body"));
            WordFixture.AddEmbeddedObject(main);
        });
        using MemoryStream input = new(docx);

        RedactedDocument result = await Create().RedactAsync(input, "memo.docx", null, new RedactionOptions());

        Assert.Single(result.Warnings);
        using MemoryStream plain = new(Encoding.UTF8.GetBytes("nothing"));
        Assert.Empty((await Create().RedactAsync(plain, "a.txt", "text/plain", new RedactionOptions())).Warnings);
    }
}
