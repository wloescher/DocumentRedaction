using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Documents;

/// <summary>
/// Output of an <see cref="IDocumentProcessor"/>: the redacted bytes, what was redacted, and
/// warnings about content that was left as it was (an embedded object nobody scanned).
/// </summary>
public sealed record ProcessedDocument(ReadOnlyMemory<byte> Content, RedactionReport Report, IReadOnlyList<string> Warnings)
{
    public ProcessedDocument(ReadOnlyMemory<byte> content, RedactionReport report)
        : this(content, report, [])
    {
    }
}

/// <summary>Output of <see cref="IDocumentRedactionService"/>, ready to be returned as a download.</summary>
public sealed record RedactedDocument(ReadOnlyMemory<byte> Content, string ContentType, string FileName, RedactionReport Report, IReadOnlyList<string> Warnings);
