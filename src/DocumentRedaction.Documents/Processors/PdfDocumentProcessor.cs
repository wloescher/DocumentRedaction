using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;
using DocumentRedaction.Documents.Processors.Pdf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Exceptions;

namespace DocumentRedaction.Documents.Processors;

/// <summary>
/// PDF files. Text is extracted with PdfPig together with every glyph's position, redacted one
/// layout block at a time, and drawn again at the same positions on pages of the original size,
/// with a labelled black box over each redacted span. The original text is therefore gone
/// rather than hidden. Fonts are substituted and images are not carried over. PDFs with no text
/// layer are rejected, as are PDFs over the <see cref="DocumentLimits"/> page, text or
/// decompressed-stream caps.
/// </summary>
public sealed class PdfDocumentProcessor : IDocumentProcessor
{
    private readonly ITextRedactor _redactor;
    private readonly DocumentLimits _limits;

    /// <summary><paramref name="license"/> is applied to QuestPDF's process-wide settings; see <see cref="QuestPdfSettings"/>.</summary>
    public PdfDocumentProcessor(ITextRedactor redactor, DocumentLimits? limits = null, QuestPdfLicense license = QuestPdfLicense.Community)
    {
        ArgumentNullException.ThrowIfNull(redactor);
        _redactor = redactor;
        _limits = DocumentLimits.Validated(limits);
        QuestPdfSettings.Apply(license);
    }

    public DocumentFormat Format => DocumentFormat.Pdf;

    public IReadOnlySet<string> Extensions { get; } = new HashSet<string>(StringComparer.Ordinal) { ".pdf" };

    public IReadOnlySet<string> ContentTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { "application/pdf" };

    public string OutputExtension => ".pdf";

    public string OutputContentType => "application/pdf";

    public ProcessedDocument Redact(ReadOnlyMemory<byte> content, RedactionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<PdfPage> pages = Extract(content, cancellationToken);
        if (pages.TrueForAll(page => page.Blocks.Count == 0))
        {
            throw new EmptyDocumentException(
                "The PDF contains no extractable text. Scanned documents need OCR before they can be redacted.");
        }

        PdfBlockRedactor blockRedactor = new(_redactor, options);
        RedactionReport report = RedactionReport.Empty;
        List<PdfRedactedPage> redacted = [];
        foreach (PdfPage page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<PdfTextRun> runs = [];
            List<PdfRedactionBox> boxes = [];
            foreach (PdfBlock block in page.Blocks)
            {
                PdfBlockRedactor.BlockResult result = blockRedactor.Redact(block);
                report = report.Merge(RedactionReport.FromDetections(result.Detections));
                runs.AddRange(result.Runs);
                boxes.AddRange(result.Boxes);
            }

            redacted.Add(new PdfRedactedPage(page.Width, page.Height, runs, boxes));
        }

        return new ProcessedDocument(Render(redacted), report);
    }

    /// <summary>Drawing can fail on degenerate geometry (a zero-sized media box); that is the file's fault, not a server error.</summary>
    private static byte[] Render(List<PdfRedactedPage> pages)
    {
        try
        {
            return PdfSvgRenderer.Render(pages);
        }
        catch (Exception ex) when (ex is not (DocumentRedactionException or OperationCanceledException or OutOfMemoryException))
        {
            throw new InvalidDocumentException("The PDF could not be rebuilt after redaction; its page geometry is not usable.", ex);
        }
    }

    private List<PdfPage> Extract(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        BoundedFilterProvider filters = new(_limits.MaxDecodedBytes, _limits.MaxTotalDecodedBytes);
        ParsingOptions parsingOptions = new() { FilterProvider = filters };
        try
        {
            using PdfDocument document = PdfDocument.Open(content, parsingOptions);

            // PdfPig swallows filter errors while reading xref streams and falls back to a scan.
            filters.ThrowIfLimitExceeded();

            // The count is the walked page tree, so it is exactly what GetPages will yield.
            if (document.NumberOfPages > _limits.MaxPdfPages)
            {
                throw new DocumentLimitExceededException(
                    $"The PDF has {document.NumberOfPages:N0} pages; the limit is {_limits.MaxPdfPages:N0}.");
            }

            List<PdfPage> pages = [];
            long characters = 0;
            foreach (Page page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                PdfPage layout = PdfLayoutExtractor.Extract(page);
                filters.ThrowIfLimitExceeded();

                foreach (PdfBlock block in layout.Blocks)
                {
                    characters += block.Text.Length;
                }

                if (characters > _limits.MaxTextCharacters)
                {
                    throw new DocumentLimitExceededException(
                        $"The text in the PDF exceeds the limit of {_limits.MaxTextCharacters:N0} characters.");
                }

                pages.Add(layout);
            }

            return pages;
        }
        catch (Exception ex) when (filters.Rejection is { } rejection
            && ex is not (DocumentLimitExceededException or OperationCanceledException or OutOfMemoryException))
        {
            // PdfPig wrapped or swallowed the filter's rejection; surface the cap, not a parse error.
            throw new DocumentLimitExceededException(rejection.Message, ex);
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
}
