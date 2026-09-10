using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.Exceptions;

namespace DocumentRedaction.Documents.Processors;

/// <summary>
/// PDF files. Text is extracted with PdfPig, grouped into paragraph blocks in reading order,
/// redacted, and written to a brand-new PDF with QuestPDF. The original text is therefore
/// gone rather than hidden, at the cost of original fonts, images and column layout.
/// Page sizes and page count are carried over. PDFs with no text layer are rejected.
/// </summary>
public sealed class PdfDocumentProcessor : IDocumentProcessor
{
    private const float MarginPoints = 40;
    private const float FontSizePoints = 11;
    private const float ParagraphSpacingPoints = 8;

    private readonly ITextRedactor _redactor;

    static PdfDocumentProcessor()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // Redacted documents may contain glyphs outside the bundled font; render what we can.
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    public PdfDocumentProcessor(ITextRedactor redactor)
    {
        _redactor = redactor;
    }

    public DocumentFormat Format => DocumentFormat.Pdf;

    public IReadOnlySet<string> Extensions { get; } = new HashSet<string>(StringComparer.Ordinal) { ".pdf" };

    public IReadOnlySet<string> ContentTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { "application/pdf" };

    public string OutputExtension => ".pdf";

    public string OutputContentType => "application/pdf";

    public ProcessedDocument Redact(ReadOnlyMemory<byte> content, RedactionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<PageContent> pages = Extract(content, cancellationToken);
        if (pages.TrueForAll(page => page.Paragraphs.Count == 0))
        {
            throw new EmptyDocumentException(
                "The PDF contains no extractable text. Scanned documents need OCR before they can be redacted.");
        }

        RedactionReport report = RedactionReport.Empty;
        List<PageContent> redacted = [];
        foreach (PageContent page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<string> paragraphs = [];
            foreach (string paragraph in page.Paragraphs)
            {
                TextRedactionResult result = _redactor.Redact(paragraph, options);
                report = report.Merge(result.Report);
                paragraphs.Add(result.RedactedText);
            }

            redacted.Add(page with { Paragraphs = paragraphs });
        }

        return new ProcessedDocument(Render(redacted), report);
    }

    private static List<PageContent> Extract(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        try
        {
            using PdfDocument document = PdfDocument.Open(content.ToArray());
            List<PageContent> pages = [];
            foreach (Page page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                pages.Add(new PageContent((float)page.Width, (float)page.Height, ExtractParagraphs(page)));
            }

            return pages;
        }
        catch (PdfDocumentEncryptedException ex)
        {
            throw new InvalidDocumentException("The PDF is password-protected. Remove the password and try again.", ex);
        }
        catch (Exception ex) when (ex is not (DocumentRedactionException or OperationCanceledException or OutOfMemoryException))
        {
            throw new InvalidDocumentException("The file is not a valid PDF document.", ex);
        }
    }

    /// <summary>
    /// Groups words into blocks (Docstrum), orders the blocks as a reader would, and joins each
    /// block's lines with spaces so that an identifier wrapped across lines is still one token.
    /// </summary>
    private static List<string> ExtractParagraphs(Page page)
    {
        List<Word> words = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();
        if (words.Count == 0)
        {
            return [];
        }

        IReadOnlyList<TextBlock> blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
        return UnsupervisedReadingOrderDetector.Instance.Get(blocks)
            .OrderBy(block => block.ReadingOrder)
            .Select(block => string.Join(' ', block.TextLines.Select(line => line.Text.Trim())).Trim())
            .Where(paragraph => paragraph.Length > 0)
            .ToList();
    }

    private static byte[] Render(List<PageContent> pages) =>
        Document.Create(container =>
        {
            foreach (PageContent page in pages)
            {
                container.Page(descriptor =>
                {
                    descriptor.Size(page.Width, page.Height, Unit.Point);
                    descriptor.Margin(MarginPoints, Unit.Point);
                    descriptor.DefaultTextStyle(style => style.FontSize(FontSizePoints));
                    descriptor.Content().Column(column =>
                    {
                        column.Spacing(ParagraphSpacingPoints, Unit.Point);
                        foreach (string paragraph in page.Paragraphs)
                        {
                            column.Item().Text(paragraph);
                        }
                    });
                });
            }
        }).GeneratePdf();

    private sealed record PageContent(float Width, float Height, List<string> Paragraphs);
}
