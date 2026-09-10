using DocumentRedaction.Core.Redaction;
using DocumentRedaction.Documents.Processors;

namespace DocumentRedaction.Documents.Tests;

public class DocumentProcessorResolverTests
{
    private static DocumentProcessorResolver Create()
    {
        TextRedactor redactor = TextRedactor.CreateDefault();
        return new DocumentProcessorResolver([new TextDocumentProcessor(redactor), new WordDocumentProcessor(redactor), new PdfDocumentProcessor(redactor)]);
    }

    [Theory]
    [InlineData("a.txt", null, DocumentFormat.PlainText)]
    [InlineData("a.TXT", null, DocumentFormat.PlainText)]
    [InlineData("notes.md", null, DocumentFormat.PlainText)]
    [InlineData("a.docx", null, DocumentFormat.Word)]
    [InlineData("a.pdf", "application/octet-stream", DocumentFormat.Pdf)]
    [InlineData("noext", "text/plain", DocumentFormat.PlainText)]
    [InlineData("noext", "text/plain; charset=utf-8", DocumentFormat.PlainText)]
    [InlineData("noext", "application/pdf", DocumentFormat.Pdf)]
    [InlineData("noext", "APPLICATION/PDF", DocumentFormat.Pdf)]
    [InlineData("a.bin", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", DocumentFormat.Word)]
    public void Resolves_by_extension_then_content_type(string fileName, string? contentType, DocumentFormat expected) =>
        Assert.Equal(expected, Create().Resolve(fileName, contentType).Format);

    [Theory]
    [InlineData("a.doc", null)]
    [InlineData("a.xlsx", "application/octet-stream")]
    [InlineData("noext", null)]
    [InlineData("noext", "")]
    [InlineData("noext", "image/png")]
    public void Unknown_inputs_throw(string fileName, string? contentType)
    {
        UnsupportedDocumentFormatException ex = Assert.Throws<UnsupportedDocumentFormatException>(() => Create().Resolve(fileName, contentType));
        Assert.Contains(".docx", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Lists_supported_extensions_sorted() =>
        Assert.Equal([".csv", ".docx", ".log", ".md", ".pdf", ".text", ".txt"], Create().SupportedExtensions);

    [Fact]
    public void Duplicate_extension_registrations_are_rejected()
    {
        TextRedactor redactor = TextRedactor.CreateDefault();
        Assert.Throws<ArgumentException>(() => new DocumentProcessorResolver([new PdfDocumentProcessor(redactor), new PdfDocumentProcessor(redactor)]));
    }
}
