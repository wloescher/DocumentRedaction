namespace DocumentRedaction.Web.Api;

/// <summary>Rejects <c>/api</c> requests without a valid key before any form data is read.</summary>
public sealed class ApiKeyEndpointFilter : IEndpointFilter
{
    private readonly ApiKeyAuthenticator _authenticator;

    public ApiKeyEndpointFilter(ApiKeyAuthenticator authenticator)
    {
        ArgumentNullException.ThrowIfNull(authenticator);
        _authenticator = authenticator;
    }

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        return _authenticator.TryAuthenticate(context.HttpContext, out _)
            ? next(context)
            : ValueTask.FromResult<object?>(ApiProblems.Unauthorized());
    }
}
