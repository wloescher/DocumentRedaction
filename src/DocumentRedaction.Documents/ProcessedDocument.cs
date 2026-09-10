using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Documents;

/// <summary>Output of an <see cref="IDocumentProcessor"/>: the redacted bytes and what was redacted.</summary>
public sealed record ProcessedDocument(ReadOnlyMemory<byte> Content, RedactionReport Report);

/// <summary>Output of <see cref="IDocumentRedactionService"/>, ready to be returned as a download.</summary>
public sealed record RedactedDocument(ReadOnlyMemory<byte> Content, string ContentType, string FileName, RedactionReport Report);
