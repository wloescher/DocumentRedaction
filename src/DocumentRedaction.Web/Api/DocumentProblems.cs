using DocumentRedaction.Documents;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DocumentRedaction.Web.Api;

/// <summary>Maps document errors to HTTP problem responses in one place.</summary>
public static class DocumentProblems
{
    public static int StatusCodeFor(DocumentRedactionException exception) => exception switch
    {
        UnsupportedDocumentFormatException => StatusCodes.Status415UnsupportedMediaType,
        InvalidDocumentException => StatusCodes.Status422UnprocessableEntity,
        EmptyDocumentException => StatusCodes.Status422UnprocessableEntity,
        DocumentLimitExceededException => StatusCodes.Status413PayloadTooLarge,
        _ => StatusCodes.Status400BadRequest,
    };

    public static ProblemHttpResult FromException(DocumentRedactionException exception) =>
        TypedResults.Problem(detail: exception.Message, statusCode: StatusCodeFor(exception), title: "The document could not be redacted.");

    public static ProblemHttpResult TooLarge(long maxBytes) =>
        TypedResults.Problem(
            detail: $"The file exceeds the maximum upload size of {maxBytes:N0} bytes.",
            statusCode: StatusCodes.Status413PayloadTooLarge,
            title: "File too large.");
}
