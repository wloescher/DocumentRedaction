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
/// Page sizes and page count are carried over. PDFs with no text layer are rejected, as are
/// PDFs that exceed the <see cref="DocumentLimits"/> page, text or decompressed-stream caps.
/// </summary>
public sealed class PdfDocumentProcessor : IDocumentProcessor
{
    private const float MarginPoints = 40;
    private const float FontSizePoints = 11;
    private const float ParagraphSpacingPoints = 8;

    private readonly ITextRedactor _redactor;
    private readonly DocumentLimits _limits;

    static PdfDocumentProcessor()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // Redacted documents may contain glyphs outside the bundled font; render what we can.
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    public PdfDocumentProcessor(ITextRedactor redactor, DocumentLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(redactor);
        _redactor = redactor;
        _limits = DocumentLimits.Validated(limits);
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

        // Built once: the custom-term detector caches its regex per options instance.
        RedactionOptions tokenOptions = options with { ExcludedKinds = options.ExcludedKinds.Union(InformationKinds.SentenceKinds).ToHashSet() };

        RedactionReport report = RedactionReport.Empty;
        List<PageContent> redacted = [];
        foreach (PageContent page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<string> paragraphs = [];
            foreach (string paragraph in page.Paragraphs)
            {
                IReadOnlyList<Detection> detections = DetectBlock(paragraph, options, tokenOptions);
                report = report.Merge(RedactionReport.FromDetections(detections));
                paragraphs.Add(_redactor.Apply(paragraph, detections, options));
            }

            redacted.Add(page with { Paragraphs = paragraphs });
        }

        return new ProcessedDocument(Render(redacted), report);
    }

    /// <summary>
    /// A block's lines are joined with newlines so sentence-widening detectors stop at a line
    /// break (a confidentiality marker in a bullet list must not swallow the whole list). A second
    /// pass over the same text with spaces instead finds an identifier wrapped across two lines,
    /// such as a card number; <paramref name="tokenOptions"/> leaves the sentence kinds out of it,
    /// or they would widen to the block. Both strings have identical offsets, so the two sets
    /// merge directly. Before resolving overlaps, every sentence is widened over each wrapped
    /// token (and other sentence) it touches, so no half of a token survives on a neighbouring
    /// line and the sentence cannot lose the overlap to a longer token.
    /// </summary>
    private IReadOnlyList<Detection> DetectBlock(string block, RedactionOptions options, RedactionOptions tokenOptions)
    {
        IReadOnlyList<Detection> byLine = _redactor.Detect(block, options);
        if (!block.Contains('\n', StringComparison.Ordinal))
        {
            return byLine;
        }

        IReadOnlyList<Detection> acrossLines = _redactor.Detect(block.Replace('\n', ' '), tokenOptions);
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
            for (int i = 0; i < sentences.Count; i++)
            {
                foreach (Detection other in tokens.Concat(sentences.ToArray()))
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

    private List<PageContent> Extract(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
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

            List<PageContent> pages = [];
            long characters = 0;
            foreach (Page page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<string> paragraphs = ExtractParagraphs(page);
                filters.ThrowIfLimitExceeded();

                foreach (string paragraph in paragraphs)
                {
                    characters += paragraph.Length;
                }

                if (characters > _limits.MaxTextCharacters)
                {
                    throw new DocumentLimitExceededException(
                        $"The text in the PDF exceeds the limit of {_limits.MaxTextCharacters:N0} characters.");
                }

                pages.Add(new PageContent((float)page.Width, (float)page.Height, paragraphs));
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

    /// <summary>
    /// Groups words into blocks (Docstrum), orders the blocks as a reader would, and joins each
    /// block's lines with newlines; see <see cref="DetectBlock"/> for how both line-bound and
    /// line-spanning detections are found on that text.
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
            .Select(block => string.Join('\n', block.TextLines.Select(line => line.Text.Trim())).Trim())
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
