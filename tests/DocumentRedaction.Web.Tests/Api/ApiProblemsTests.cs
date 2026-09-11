using DocumentRedaction.Documents;
using DocumentRedaction.Web;
using DocumentRedaction.Web.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DocumentRedaction.Web.Tests.Api;

public class ApiProblemsTests
{
    [Fact]
    public void Maps_each_exception_to_a_status()
    {
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, ApiProblems.StatusCodeFor(new UnsupportedDocumentFormatException("x")));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, ApiProblems.StatusCodeFor(new InvalidDocumentException("x")));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, ApiProblems.StatusCodeFor(new EmptyDocumentException("x")));
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, ApiProblems.StatusCodeFor(new DocumentLimitExceededException("x")));
        Assert.Equal(StatusCodes.Status400BadRequest, ApiProblems.StatusCodeFor(new DocumentRedactionException("x")));
    }

    [Fact]
    public void Problem_carries_message_as_detail()
    {
        ProblemHttpResult problem = ApiProblems.FromException(new InvalidDocumentException("bad file"));
        Assert.Equal("bad file", problem.ProblemDetails.Detail);
        Assert.Equal(422, problem.StatusCode);
    }

    [Fact]
    public void Too_large_states_the_limit()
    {
        ProblemHttpResult problem = ApiProblems.TooLarge(1_000);
        Assert.Equal(413, problem.StatusCode);
        Assert.Contains("1,000", problem.ProblemDetails.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Unauthorized_names_the_header()
    {
        ProblemHttpResult problem = ApiProblems.Unauthorized();
        Assert.Equal(401, problem.StatusCode);
        Assert.Contains(ApiKeyAuthenticator.HeaderName, problem.ProblemDetails.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Too_many_requests_states_the_window()
    {
        ProblemHttpResult problem = ApiProblems.TooManyRequests(new RateLimitSettings { PermitLimit = 5, WindowSeconds = 30 });
        Assert.Equal(429, problem.StatusCode);
        Assert.Contains("5 requests per 30 seconds", problem.ProblemDetails.Detail, StringComparison.Ordinal);
    }
}
