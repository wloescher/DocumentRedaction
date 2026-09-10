namespace DocumentRedaction.Documents;

/// <summary>Chooses the processor for an uploaded file.</summary>
public interface IDocumentProcessorResolver
{
    IReadOnlyList<IDocumentProcessor> Processors { get; }

    /// <summary>Every extension any processor accepts, for validation messages and UI hints.</summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Resolves by file extension first, then by content type. Throws
    /// <see cref="UnsupportedDocumentFormatException"/> when neither is recognised.
    /// </summary>
    IDocumentProcessor Resolve(string fileName, string? contentType);
}

public sealed class DocumentProcessorResolver : IDocumentProcessorResolver
{
    private readonly Dictionary<string, IDocumentProcessor> _byExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IDocumentProcessor> _byContentType = new(StringComparer.OrdinalIgnoreCase);

    public DocumentProcessorResolver(IEnumerable<IDocumentProcessor> processors)
    {
        Processors = processors.ToList();
        foreach (IDocumentProcessor processor in Processors)
        {
            foreach (string extension in processor.Extensions)
            {
                _byExtension.Add(extension, processor);
            }

            foreach (string contentType in processor.ContentTypes)
            {
                _byContentType.Add(contentType, processor);
            }
        }

        SupportedExtensions = _byExtension.Keys.Order(StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<IDocumentProcessor> Processors { get; }

    public IReadOnlyList<string> SupportedExtensions { get; }

    public IDocumentProcessor Resolve(string fileName, string? contentType)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        string extension = Path.GetExtension(fileName);
        if (extension.Length > 0 && _byExtension.TryGetValue(extension, out IDocumentProcessor? byExtension))
        {
            return byExtension;
        }

        // Media types may carry parameters ("text/plain; charset=utf-8"); match on the type alone.
        string? mediaType = contentType?.Split(';', 2)[0].Trim();
        if (!string.IsNullOrEmpty(mediaType) && _byContentType.TryGetValue(mediaType, out IDocumentProcessor? byContentType))
        {
            return byContentType;
        }

        throw new UnsupportedDocumentFormatException(
            $"Unsupported file type '{(extension.Length > 0 ? extension : mediaType ?? "unknown")}'. Supported: {string.Join(", ", SupportedExtensions)}.");
    }
}
