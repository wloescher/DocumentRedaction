namespace DocumentRedaction.Documents;

/// <summary>Base for errors that stem from the document itself rather than the service.</summary>
public class DocumentRedactionException : Exception
{
    public DocumentRedactionException()
    {
    }

    public DocumentRedactionException(string message)
        : base(message)
    {
    }

    public DocumentRedactionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The file's extension and content type do not map to any processor.</summary>
public sealed class UnsupportedDocumentFormatException : DocumentRedactionException
{
    public UnsupportedDocumentFormatException()
    {
    }

    public UnsupportedDocumentFormatException(string message)
        : base(message)
    {
    }

    public UnsupportedDocumentFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The file could not be parsed: corrupt, encrypted, or not the format its name claims.</summary>
public sealed class InvalidDocumentException : DocumentRedactionException
{
    public InvalidDocumentException()
    {
    }

    public InvalidDocumentException(string message)
        : base(message)
    {
    }

    public InvalidDocumentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The file parsed but contains no text to redact, such as a scanned PDF without a text layer.</summary>
public sealed class EmptyDocumentException : DocumentRedactionException
{
    public EmptyDocumentException()
    {
    }

    public EmptyDocumentException(string message)
        : base(message)
    {
    }

    public EmptyDocumentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
