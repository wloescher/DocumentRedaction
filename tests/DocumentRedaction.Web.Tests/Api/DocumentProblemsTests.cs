using DocumentRedaction.Documents;
using DocumentRedaction.Web.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DocumentRedaction.Web.Tests.Api;

public class DocumentProblemsTests
{
    [Fact]
    public void Maps_each_exception_to_a_status()
    {
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, DocumentProblems.StatusCodeFor(new UnsupportedDocumentFormatException("x")));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, DocumentProblems.StatusCodeFor(new InvalidDocumentException("x")));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, DocumentProblems.StatusCodeFor(new EmptyDocumentException("x")));
        Assert.Equal(StatusCodes.Status400BadRequest, DocumentProblems.StatusCodeFor(new DocumentRedactionException("x")));
    }

    [Fact]
    public void Problem_carries_message_as_detail()
    {
        ProblemHttpResult problem = DocumentProblems.FromException(new InvalidDocumentException("bad file"));
        Assert.Equal("bad file", problem.ProblemDetails.Detail);
        Assert.Equal(422, problem.StatusCode);
    }

    [Fact]
    public void Too_large_states_the_limit()
    {
        ProblemHttpResult problem = DocumentProblems.TooLarge(1_000);
        Assert.Equal(413, problem.StatusCode);
        Assert.Contains("1,000", problem.ProblemDetails.Detail, StringComparison.Ordinal);
    }
}
