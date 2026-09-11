namespace DocumentRedaction.Web.Api;

/// <summary>
/// Runs once at startup: fails the host when <c>Redaction:ApiKeys</c> was given as a single value
/// (the binder silently ignores a scalar where a list is expected, which would leave the API
/// open by accident), and otherwise logs a warning when no key is configured so open mode is a
/// choice rather than an oversight.
/// </summary>
public sealed partial class ApiKeyStartupCheck : IHostedService
{
    private const string ApiKeysSection = RedactionSettings.SectionName + ":" + nameof(RedactionSettings.ApiKeys);

    private readonly ApiKeyAuthenticator _authenticator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyStartupCheck> _logger;

    public ApiKeyStartupCheck(ApiKeyAuthenticator authenticator, IConfiguration configuration, ILogger<ApiKeyStartupCheck> logger)
    {
        ArgumentNullException.ThrowIfNull(authenticator);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        _authenticator = authenticator;
        _configuration = configuration;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // A JSON "[]" leaves the section with a null value; only a scalar string is the mistake.
        if (!string.IsNullOrEmpty(_configuration.GetSection(ApiKeysSection).Value))
        {
            throw new InvalidOperationException(
                $"{ApiKeysSection} must be a list (for example {RedactionSettings.SectionName}__{nameof(RedactionSettings.ApiKeys)}__0 as an environment variable), not a single value.");
        }

        if (_authenticator.IsOpen)
        {
            LogOpenApi(_logger);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "No Redaction:ApiKeys are configured, so /api accepts requests from anyone. Configure at least one key before exposing this host.")]
    private static partial void LogOpenApi(ILogger logger);
}
