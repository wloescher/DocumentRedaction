using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Documents;

/// <summary>Reads one document format, redacts its text, and writes the same format back.</summary>
public interface IDocumentProcessor
{
    DocumentFormat Format { get; }

    /// <summary>Lower-case extensions including the dot, e.g. ".docx".</summary>
    IReadOnlySet<string> Extensions { get; }

    /// <summary>Lower-case media types this processor accepts and produces.</summary>
    IReadOnlySet<string> ContentTypes { get; }

    /// <summary>Extension used for output files.</summary>
    string OutputExtension { get; }

    /// <summary>Media type of the output.</summary>
    string OutputContentType { get; }

    /// <summary>CPU-bound; callers that must not block should run it on a worker thread.</summary>
    ProcessedDocument Redact(ReadOnlyMemory<byte> content, RedactionOptions options, CancellationToken cancellationToken = default);
}
