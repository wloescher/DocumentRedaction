using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace DocumentRedaction.Web.Tests.Api;

/// <summary>Hosts the application in-process with a small upload limit so size handling can be tested.</summary>
public sealed class RedactionApiFixture : WebApplicationFactory<Program>
{
    public const long MaxUploadBytes = 200_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redaction:MaxUploadBytes"] = MaxUploadBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }));
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
