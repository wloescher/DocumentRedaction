using System.Text;
using DocumentRedaction.Core.Model;
using DocumentRedaction.Core.Redaction;

namespace DocumentRedaction.Documents.Processors;

/// <summary>
/// Plain-text files. The whole file is redacted as one string, so line endings are preserved
/// verbatim. Input encoding is detected from a byte-order mark and defaults to UTF-8; output
/// is written in the same encoding, keeping the BOM only if the input had one.
/// </summary>
public sealed class TextDocumentProcessor : IDocumentProcessor
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private readonly ITextRedactor _redactor;

    public TextDocumentProcessor(ITextRedactor redactor)
    {
        _redactor = redactor;
    }

    public DocumentFormat Format => DocumentFormat.PlainText;

    public IReadOnlySet<string> Extensions { get; } = new HashSet<string>(StringComparer.Ordinal) { ".txt", ".text", ".md", ".csv", ".log" };

    public IReadOnlySet<string> ContentTypes { get; } = new HashSet<string>(StringComparer.Ordinal) { "text/plain", "text/markdown", "text/csv" };

    public string OutputExtension => ".txt";

    /// <summary>No charset parameter: the output keeps the input's encoding, which may not be UTF-8.</summary>
    public string OutputContentType => "text/plain";

    public ProcessedDocument Redact(ReadOnlyMemory<byte> content, RedactionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        (string text, Encoding encoding) = Decode(content);
        TextRedactionResult result = _redactor.Redact(text, options);
        byte[] bytes = [.. encoding.GetPreamble(), .. encoding.GetBytes(result.RedactedText)];
        return new ProcessedDocument(bytes, result.Report);
    }

    private static (string Text, Encoding Encoding) Decode(ReadOnlyMemory<byte> content)
    {
        using MemoryStream stream = new(content.ToArray(), writable: false);
        using StreamReader reader = new(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: true);
        string text = reader.ReadToEnd();
        // CurrentEncoding reflects the BOM that was detected (UTF-8 with BOM, UTF-16, UTF-32).
        Encoding encoding = ReferenceEquals(reader.CurrentEncoding, Utf8NoBom) ? Utf8NoBom : reader.CurrentEncoding;
        return (text, encoding);
    }
}
