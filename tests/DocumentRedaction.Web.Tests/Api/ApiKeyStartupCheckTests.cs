using DocumentRedaction.Web.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentRedaction.Web.Tests.Api;

public class ApiKeyStartupCheckTests
{
    private static ApiKeyStartupCheck Check(CapturingLogger logger, Dictionary<string, string?>? configuration = null, params string[] keys)
    {
        RedactionSettings settings = new();
        foreach (string key in keys)
        {
            settings.ApiKeys.Add(key);
        }

        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(configuration ?? []).Build();
        return new ApiKeyStartupCheck(new ApiKeyAuthenticator(Options.Create(settings)), config, logger);
    }

    [Fact]
    public async Task Open_api_logs_one_warning_naming_the_setting()
    {
        CapturingLogger logger = new();
        await Check(logger).StartAsync(TestContext.Current.CancellationToken);

        (LogLevel level, string message) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains("Redaction:ApiKeys", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Configured_keys_log_nothing()
    {
        CapturingLogger logger = new();
        await Check(logger, keys: "configured-key-0123456789").StartAsync(TestContext.Current.CancellationToken);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task Scalar_api_keys_value_fails_startup_without_echoing_it()
    {
        // The binder ignores a scalar where a list is expected, so the authenticator would be open.
        CapturingLogger logger = new();
        ApiKeyStartupCheck check = Check(logger, new Dictionary<string, string?> { ["Redaction:ApiKeys"] = "single-value-0123456789" });

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => check.StartAsync(TestContext.Current.CancellationToken));
        Assert.Contains("Redaction:ApiKeys must be a list", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("single-value", ex.Message, StringComparison.Ordinal);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task Empty_json_array_leaves_a_null_section_value_and_is_not_a_scalar()
    {
        CapturingLogger logger = new();
        await Check(logger, new Dictionary<string, string?> { ["Redaction:ApiKeys"] = null }).StartAsync(TestContext.Current.CancellationToken);
        Assert.Single(logger.Entries);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        ApiKeyAuthenticator authenticator = new(Options.Create(new RedactionSettings()));
        Assert.Throws<ArgumentNullException>(() => new ApiKeyStartupCheck(null!, config, new CapturingLogger()));
        Assert.Throws<ArgumentNullException>(() => new ApiKeyStartupCheck(authenticator, null!, new CapturingLogger()));
        Assert.Throws<ArgumentNullException>(() => new ApiKeyStartupCheck(authenticator, config, null!));
    }

    private sealed class CapturingLogger : ILogger<ApiKeyStartupCheck>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
