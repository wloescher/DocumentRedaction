using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentRedaction.Web.Tests.Api;

/// <summary>Hosts the application in-process with a small upload limit so size handling can be tested.</summary>
public sealed class RedactionApiFixture : WebApplicationFactory<Program>
{
    public const long MaxUploadBytes = 200_000;
    public const int MaxPdfPages = 2;

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        ConfigureHost(builder, new Dictionary<string, string?>
        {
            ["Redaction:MaxUploadBytes"] = MaxUploadBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Redaction:Limits:MaxPdfPages"] = MaxPdfPages.ToString(System.Globalization.CultureInfo.InvariantCulture),
            // Every test in the class shares one host and one client address; the throttle must not trip here.
            ["Redaction:RateLimit:PermitLimit"] = "100000",
        });

    /// <summary>Development environment plus the given settings layered over appsettings.json.</summary>
    public static void ConfigureHost(IWebHostBuilder builder, Dictionary<string, string?> settings)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
    }

    /// <summary>Asserts a problem-details response with the expected status and returns its body.</summary>
    public static async Task<ProblemDetails> Problem(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json, TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Equal((int)expected, problem.Status);
        return problem;
    }

    /// <summary>Builds the multipart body the /api/redact endpoints expect.</summary>
    public static MultipartFormDataContent Form(
        byte[]? file,
        string fileName = "input.txt",
        string contentType = "text/plain",
        IEnumerable<string>? categories = null,
        IEnumerable<string>? excludedKinds = null,
        IEnumerable<string>? customTerms = null,
        string? placeholderFormat = null,
        bool? caseSensitive = null)
    {
        MultipartFormDataContent form = [];
        if (file is not null)
        {
            ByteArrayContent content = new(file);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(content, "file", fileName);
        }

        foreach (string category in categories ?? ["all"])
        {
            form.Add(new StringContent(category), "categories");
        }

        foreach (string kind in excludedKinds ?? [])
        {
            form.Add(new StringContent(kind), "excludedKinds");
        }

        foreach (string term in customTerms ?? [])
        {
            form.Add(new StringContent(term), "customTerms");
        }

        if (placeholderFormat is not null)
        {
            form.Add(new StringContent(placeholderFormat), "placeholderFormat");
        }

        if (caseSensitive is { } sensitive)
        {
            form.Add(new StringContent(sensitive ? "true" : "false"), "customTermsAreCaseSensitive");
        }

        return form;
    }
}

/// <summary>A host with the given settings layered over appsettings.json, for one-off configurations (invalid ones included).</summary>
public sealed class ConfiguredFixture : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _settings;
    private readonly Action<IServiceCollection>? _services;

    public ConfiguredFixture(Dictionary<string, string?> settings, Action<IServiceCollection>? services = null)
    {
        _settings = settings;
        _services = services;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        RedactionApiFixture.ConfigureHost(builder, _settings);
        if (_services is not null)
        {
            builder.ConfigureServices(_services);
        }
    }
}
