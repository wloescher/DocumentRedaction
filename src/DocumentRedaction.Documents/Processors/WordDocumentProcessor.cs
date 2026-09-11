using System.IO.Compression;
using System.Text;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;

namespace DocumentRedaction.Documents.Processors;

/// <summary>
/// Word (.docx) files via the Open XML SDK. Each paragraph's text is assembled from its runs,
/// detected as one string so that identifiers split across runs are still found, and then
/// spliced back run by run so that formatting survives. Covers the body (including tables and
/// text boxes), headers, footers, footnotes, endnotes and comments. Tracked-change deleted
/// text and field codes are redacted too, because they are still readable in the file, and
/// external hyperlink targets that contain sensitive values are replaced with "about:blank".
/// </summary>
public sealed class WordDocumentProcessor : IDocumentProcessor
{
    private static readonly Uri BlankTarget = new("about:blank");

    private readonly ITextRedactor _redactor;
    private readonly DocumentLimits _limits;

    public WordDocumentProcessor(ITextRedactor redactor, DocumentLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(redactor);
        _redactor = redactor;
        _limits = DocumentLimits.Validated(limits);
    }

    public DocumentFormat Format => DocumentFormat.Word;

    public IReadOnlySet<string> Extensions { get; } = new HashSet<string>(StringComparer.Ordinal) { ".docx" };

    public IReadOnlySet<string> ContentTypes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    public string OutputExtension => ".docx";

    public string OutputContentType => "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public ProcessedDocument Redact(ReadOnlyMemory<byte> content, RedactionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        using MemoryStream stream = new();
        stream.Write(content.Span);
        stream.Position = 0;

        RedactionReport report = RedactionReport.Empty;
        IReadOnlyList<string> warnings = [];
        try
        {
            ThrowIfAnyPartExceedsLimit(stream);

            // Backstop for the zip-directory check: the XML reader stops at the same cap, so a
            // forged directory still cannot expand into an unbounded in-memory tree.
            OpenSettings openSettings = new() { MaxCharactersInPart = _limits.MaxDecodedBytes };
            using WordprocessingDocument document = WordprocessingDocument.Open(stream, isEditable: true, openSettings);
            MainDocumentPart main = document.MainDocumentPart
                ?? throw new InvalidDocumentException("The Word document has no main document part.");

            foreach (OpenXmlPart part in TextBearingParts(main))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (part.RootElement is { } root)
                {
                    report = report.Merge(RedactElement(root, options, cancellationToken));
                }

                report = report.Merge(RedactHyperlinkTargets(part, options));
            }

            WordMetadataRedactor.Result metadata = new WordMetadataRedactor(_redactor, options).Redact(document, cancellationToken);
            report = report.Merge(metadata.Report);
            warnings = metadata.Warnings;

            document.Save();
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or FileFormatException or InvalidDataException or IOException or XmlException)
        {
            throw new InvalidDocumentException("The file is not a valid Word (.docx) document.", ex);
        }

        return new ProcessedDocument(stream.ToArray(), report, warnings);
    }

    /// <summary>
    /// A .docx is a zip whose central directory states each part's uncompressed size, so a part
    /// that would blow past the cap is rejected before anything is inflated. Binary parts are
    /// held to the same cap as XML parts because the package is rewritten through memory on save.
    /// </summary>
    private void ThrowIfAnyPartExceedsLimit(MemoryStream stream)
    {
        using (ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.Length > _limits.MaxDecodedBytes)
                {
                    throw new DocumentLimitExceededException(
                        $"The part '{entry.FullName}' in the Word document is {entry.Length:N0} bytes uncompressed; the limit is {_limits.MaxDecodedBytes:N0}.");
                }
            }
        }

        stream.Position = 0;
    }

    private static IEnumerable<OpenXmlPart> TextBearingParts(MainDocumentPart main)
    {
        yield return main;
        foreach (HeaderPart header in main.HeaderParts)
        {
            yield return header;
        }

        foreach (FooterPart footer in main.FooterParts)
        {
            yield return footer;
        }

        if (main.FootnotesPart is { } footnotes)
        {
            yield return footnotes;
        }

        if (main.EndnotesPart is { } endnotes)
        {
            yield return endnotes;
        }

        if (main.WordprocessingCommentsPart is { } comments)
        {
            yield return comments;
        }
    }

    /// <summary>
    /// Hyperlink targets live in the part's relationships, not its XML. A target such as
    /// "mailto:john@example.com" is sensitive in its own right, so any target with a detection
    /// is replaced wholesale; splicing a placeholder into a URL would not produce a valid one.
    /// </summary>
    private RedactionReport RedactHyperlinkTargets(OpenXmlPart part, RedactionOptions options)
    {
        List<Detection> found = [];
        foreach (HyperlinkRelationship link in part.HyperlinkRelationships.ToList())
        {
            string target = Uri.UnescapeDataString(link.Uri.OriginalString);
            IReadOnlyList<Detection> detections = _redactor.Detect(target, options);
            if (detections.Count == 0)
            {
                continue;
            }

            found.AddRange(detections);
            part.DeleteReferenceRelationship(link);
            part.AddHyperlinkRelationship(BlankTarget, isExternal: true, link.Id);
        }

        return RedactionReport.FromDetections(found);
    }

    private RedactionReport RedactElement(OpenXmlElement root, RedactionOptions options, CancellationToken cancellationToken)
    {
        RedactionReport report = RedactionReport.Empty;
        foreach (Paragraph paragraph in root.Descendants<Paragraph>().ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            report = report.Merge(RedactParagraph(paragraph, options));
        }

        return report;
    }

    private RedactionReport RedactParagraph(Paragraph paragraph, RedactionOptions options)
    {
        StringBuilder buffer = new();
        List<Segment> segments = [];
        CollectSegments(paragraph, buffer, segments);
        if (segments.Count == 0)
        {
            return RedactionReport.Empty;
        }

        string text = buffer.ToString();
        IReadOnlyList<Detection> detections = _redactor.Detect(text, options);
        if (detections.Count == 0)
        {
            return RedactionReport.Empty;
        }

        SpliceSegments(text, segments, detections, options);
        return RedactionReport.FromDetections(detections);
    }

    /// <summary>
    /// Walks a paragraph in document order, appending text-bearing leaves to the buffer and
    /// recording where each one landed. Tabs and breaks contribute whitespace so that patterns
    /// cannot join words across them. Nested paragraphs (text boxes) are skipped here because
    /// the outer enumeration visits them on their own.
    /// </summary>
    private static void CollectSegments(OpenXmlElement element, StringBuilder buffer, List<Segment> segments)
    {
        foreach (OpenXmlElement child in element.ChildElements)
        {
            switch (child)
            {
                case Paragraph:
                    break;
                case Text or DeletedText or FieldCode:
                    OpenXmlLeafTextElement leaf = (OpenXmlLeafTextElement)child;
                    segments.Add(new Segment(leaf, buffer.Length, leaf.Text.Length));
                    buffer.Append(leaf.Text);
                    break;
                case TabChar:
                    buffer.Append('\t');
                    break;
                case Break or CarriageReturn:
                    buffer.Append('\n');
                    break;
                default:
                    CollectSegments(child, buffer, segments);
                    break;
            }
        }
    }

    /// <summary>
    /// Rewrites each segment so that every detection is replaced exactly once: the placeholder
    /// is written into the segment where the detection starts, and the remainder of the
    /// detection is removed from any segments it continues into.
    /// </summary>
    private static void SpliceSegments(string text, List<Segment> segments, IReadOnlyList<Detection> detections, RedactionOptions options)
    {
        int next = 0;
        foreach (Segment segment in segments)
        {
            int segmentEnd = segment.Start + segment.Length;
            StringBuilder rewritten = new();
            int cursor = segment.Start;
            bool changed = false;

            while (next < detections.Count && detections[next].Start < segmentEnd)
            {
                Detection detection = detections[next];
                if (detection.Start > cursor)
                {
                    rewritten.Append(text, cursor, detection.Start - cursor);
                }

                if (detection.Start >= segment.Start)
                {
                    rewritten.Append(options.PlaceholderFor(detection.Kind));
                }

                cursor = Math.Min(detection.End, segmentEnd);
                changed = true;
                if (detection.End > segmentEnd)
                {
                    break;
                }

                next++;
            }

            if (!changed)
            {
                continue;
            }

            if (cursor < segmentEnd)
            {
                rewritten.Append(text, cursor, segmentEnd - cursor);
            }

            SetText(segment.Element, rewritten.ToString());
        }
    }

    private static void SetText(OpenXmlLeafTextElement element, string value)
    {
        element.Text = value;
        switch (element)
        {
            case Text text:
                text.Space = SpaceProcessingModeValues.Preserve;
                break;
            case DeletedText deleted:
                deleted.Space = SpaceProcessingModeValues.Preserve;
                break;
            case FieldCode code:
                code.Space = SpaceProcessingModeValues.Preserve;
                break;
        }
    }

    private sealed record Segment(OpenXmlLeafTextElement Element, int Start, int Length);
}
