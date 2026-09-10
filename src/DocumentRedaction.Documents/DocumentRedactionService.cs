using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Documents;

/// <summary>Entry point for callers holding an uploaded file: pick the processor, run it, name the output.</summary>
public interface IDocumentRedactionService
{
    Task<RedactedDocument> RedactAsync(Stream input, string fileName, string? contentType, RedactionOptions options, CancellationToken cancellationToken = default);
}

public sealed class DocumentRedactionService : IDocumentRedactionService
{
    public const string OutputSuffix = "-redacted";

    private readonly IDocumentProcessorResolver _resolver;

    public DocumentRedactionService(IDocumentProcessorResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<RedactedDocument> RedactAsync(Stream input, string fileName, string? contentType, RedactionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(options);

        IDocumentProcessor processor = _resolver.Resolve(fileName, contentType);

        using MemoryStream buffer = new();
        await input.CopyToAsync(buffer, cancellationToken);
        ReadOnlyMemory<byte> content = new(buffer.GetBuffer(), 0, (int)buffer.Length);

        // Processing is CPU-bound; keep it off the caller's thread (a Blazor circuit or request thread).
        ProcessedDocument processed = await Task.Run(
            () => processor.Redact(content, options, cancellationToken),
            cancellationToken);

        return new RedactedDocument(
            processed.Content,
            processor.OutputContentType,
            OutputFileName(fileName, processor.OutputExtension),
            processed.Report);
    }

    /// <summary>"report.docx" becomes "report-redacted.docx"; the processor decides the extension.</summary>
    public static string OutputFileName(string originalFileName, string outputExtension)
    {
        string stem = Path.GetFileNameWithoutExtension(originalFileName);
        if (stem.Length == 0)
        {
            stem = "document";
        }

        return stem + OutputSuffix + outputExtension;
    }
}
