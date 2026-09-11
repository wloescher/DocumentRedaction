using System.Net;
using System.Text;
using DocumentRedaction.Documents;
using DocumentRedaction.Web.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace DocumentRedaction.Web.Tests.Api;

/// <summary>API-key and throttle behaviour; each test builds its own host so limiter state never leaks between tests.</summary>
public class ApiAccessTests
{
    private const string KeyA = "key-a-0123456789abcdef";
    private const string KeyB = "key-b-0123456789abcdef";

    /// <summary>Test header that a fixture middleware copies into the connection's remote address.</summary>
    private const string AddressHeader = "X-Test-Remote-Address";

    private static Dictionary<string, string?> Keyed(int permitLimit = 100_000, bool enabled = true) => new()
    {
        ["Redaction:ApiKeys:0"] = KeyA,
        ["Redaction:ApiKeys:1"] = KeyB,
        ["Redaction:RateLimit:Enabled"] = enabled ? "true" : "false",
        ["Redaction:RateLimit:PermitLimit"] = permitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["Redaction:RateLimit:WindowSeconds"] = "60",
    };

    private static Task<HttpResponseMessage> GetCategories(HttpClient client, string? key, string? address = null) =>
        Send(client, new HttpRequestMessage(HttpMethod.Get, new Uri("/api/categories", UriKind.Relative)), key, address);

    private static Task<HttpResponseMessage> PostRedact(HttpClient client, string? key, string path = "/api/redact") =>
        Send(client, new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = RedactionApiFixture.Form(Encoding.UTF8.GetBytes("SSN 123-45-6789")),
        }, key, null);

    /// <summary>Owns the request for the whole send, so the message is not disposed under the test server.</summary>
    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpRequestMessage request, string? key, string? address)
    {
        using (request)
        {
            if (key is not null)
            {
                request.Headers.Add(ApiKeyAuthenticator.HeaderName, key);
            }

            if (address is not null)
            {
                request.Headers.Add(AddressHeader, address);
            }

            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }

    private static Task<ProblemDetails> Problem(HttpResponseMessage response, HttpStatusCode expected) =>
        RedactionApiFixture.Problem(response, expected);

    /// <summary>The test server has no real connection, so the remote address comes from a test header.</summary>
    private sealed class RemoteAddressFromHeader : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(AddressHeader, out StringValues value) && IPAddress.TryParse(value.ToString(), out IPAddress? address))
                {
                    context.Connection.RemoteIpAddress = address;
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }

    private static ConfiguredFixture WithAddresses(Dictionary<string, string?> settings) =>
        new(settings, services => services.AddSingleton<IStartupFilter, RemoteAddressFromHeader>());

    [Fact]
    public async Task Open_mode_accepts_requests_without_a_key()
    {
        using ConfiguredFixture factory = new(new Dictionary<string, string?> { ["Redaction:RateLimit:PermitLimit"] = "100" });
        using HttpClient client = factory.CreateClient();

        Assert.True(factory.Services.GetRequiredService<ApiKeyAuthenticator>().IsOpen);
        using HttpResponseMessage categories = await GetCategories(client, null);
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
        using HttpResponseMessage redact = await PostRedact(client, null);
        Assert.Equal(HttpStatusCode.OK, redact.StatusCode);
    }

    [Fact]
    public async Task Missing_key_is_401_problem_on_every_api_route()
    {
        using ConfiguredFixture factory = new(Keyed());
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage categories = await GetCategories(client, null);
        ProblemDetails problem = await Problem(categories, HttpStatusCode.Unauthorized);
        Assert.Contains(ApiKeyAuthenticator.HeaderName, problem.Detail, StringComparison.Ordinal);

        using HttpResponseMessage redact = await PostRedact(client, null);
        await Problem(redact, HttpStatusCode.Unauthorized);

        using HttpResponseMessage summary = await PostRedact(client, null, "/api/redact/summary");
        await Problem(summary, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Summary_route_accepts_a_valid_key_and_counts_against_its_window()
    {
        using ConfiguredFixture factory = new(Keyed(permitLimit: 1));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage summary = await PostRedact(client, KeyB, "/api/redact/summary");
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
        using HttpResponseMessage second = await GetCategories(client, KeyB);
        await Problem(second, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Wrong_key_is_401_and_never_echoed()
    {
        using ConfiguredFixture factory = new(Keyed());
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await PostRedact(client, "not-a-configured-key-0123");
        ProblemDetails problem = await Problem(response, HttpStatusCode.Unauthorized);
        Assert.DoesNotContain("not-a-configured-key", problem.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(KeyA, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Valid_key_redacts_and_returns_the_report()
    {
        using ConfiguredFixture factory = new(Keyed());
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage categories = await GetCategories(client, KeyB);
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
        using HttpResponseMessage redact = await PostRedact(client, KeyB);
        Assert.Equal(HttpStatusCode.OK, redact.StatusCode);
        Assert.Equal("1", redact.Headers.GetValues(RedactionEndpoints.TotalHeader).Single());
        Assert.Equal("SSN [REDACTED-SSN]", await redact.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Requests_over_the_limit_are_429_with_retry_after_and_each_key_has_its_own_window()
    {
        using ConfiguredFixture factory = new(Keyed(permitLimit: 2));
        using HttpClient client = factory.CreateClient();

        for (int i = 0; i < 2; i++)
        {
            using HttpResponseMessage ok = await GetCategories(client, KeyA);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        using HttpResponseMessage rejected = await GetCategories(client, KeyA);
        ProblemDetails problem = await Problem(rejected, HttpStatusCode.TooManyRequests);
        Assert.Contains("2 requests per 60 seconds", problem.Detail, StringComparison.Ordinal);
        Assert.NotNull(rejected.Headers.RetryAfter?.Delta);
        Assert.InRange(rejected.Headers.RetryAfter.Delta.Value, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60));

        // The other key is untouched by the first key's exhausted window.
        using HttpResponseMessage otherKey = await GetCategories(client, KeyB);
        Assert.Equal(HttpStatusCode.OK, otherKey.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_requests_share_one_window_per_address_so_key_guessing_is_throttled()
    {
        using ConfiguredFixture factory = new(Keyed(permitLimit: 2));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage firstGuess = await GetCategories(client, "guess-one-0123456789");
        Assert.Equal(HttpStatusCode.Unauthorized, firstGuess.StatusCode);
        using HttpResponseMessage secondGuess = await GetCategories(client, null);
        Assert.Equal(HttpStatusCode.Unauthorized, secondGuess.StatusCode);

        // A third guess, even with a different value, is throttled before the key is checked.
        using HttpResponseMessage thirdGuess = await GetCategories(client, "guess-two-0123456789");
        await Problem(thirdGuess, HttpStatusCode.TooManyRequests);

        // Valid keys have their own windows and are unaffected.
        using HttpResponseMessage valid = await GetCategories(client, KeyA);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task Open_mode_is_throttled_per_address()
    {
        using ConfiguredFixture factory = WithAddresses(new Dictionary<string, string?> { ["Redaction:RateLimit:PermitLimit"] = "1" });
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage first = await GetCategories(client, null, "198.51.100.1");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using HttpResponseMessage second = await GetCategories(client, null, "198.51.100.1");
        ProblemDetails problem = await Problem(second, HttpStatusCode.TooManyRequests);
        Assert.Contains("1 requests per 60 seconds", problem.Detail, StringComparison.Ordinal);
        Assert.Equal(TimeSpan.FromSeconds(60), second.Headers.RetryAfter?.Delta);

        // Another address has its own window.
        using HttpResponseMessage otherAddress = await GetCategories(client, null, "198.51.100.2");
        Assert.Equal(HttpStatusCode.OK, otherAddress.StatusCode);
    }

    [Fact]
    public async Task Missing_and_wrong_keys_from_one_address_share_that_address_window()
    {
        using ConfiguredFixture factory = WithAddresses(Keyed(permitLimit: 2));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage missing = await GetCategories(client, null, "198.51.100.1");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        using HttpResponseMessage wrong = await GetCategories(client, "guess-one-0123456789", "198.51.100.1");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        using HttpResponseMessage throttled = await GetCategories(client, "guess-two-0123456789", "198.51.100.1");
        await Problem(throttled, HttpStatusCode.TooManyRequests);

        // A different address guessing for the first time is only refused, not throttled.
        using HttpResponseMessage elsewhere = await GetCategories(client, "guess-two-0123456789", "198.51.100.2");
        Assert.Equal(HttpStatusCode.Unauthorized, elsewhere.StatusCode);
    }

    [Fact]
    public async Task Disabled_limiter_never_rejects()
    {
        using ConfiguredFixture factory = new(Keyed(permitLimit: 1, enabled: false));
        using HttpClient client = factory.CreateClient();

        for (int i = 0; i < 3; i++)
        {
            using HttpResponseMessage response = await GetCategories(client, KeyA);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task The_page_is_not_gated_or_throttled()
    {
        using ConfiguredFixture factory = new(Keyed(permitLimit: 1));
        using HttpClient client = factory.CreateClient();

        for (int i = 0; i < 3; i++)
        {
            using HttpResponseMessage page = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        }
    }

    [Fact]
    public void Short_api_key_fails_host_startup_without_echoing_it()
    {
        using ConfiguredFixture factory = new(new Dictionary<string, string?> { ["Redaction:ApiKeys:0"] = "tooshort" });
        Exception ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Redaction:ApiKeys:0", ex.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("tooshort", ex.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Freeware")]
    [InlineData("99")]
    public void Unknown_or_undefined_license_fails_host_startup(string value)
    {
        using ConfiguredFixture factory = new(new Dictionary<string, string?> { ["Redaction:QuestPdfLicense"] = value });
        Exception ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("QuestPdfLicense", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Scalar_api_keys_setting_fails_host_startup_instead_of_opening_the_api()
    {
        using ConfiguredFixture factory = new(new Dictionary<string, string?> { ["Redaction:ApiKeys"] = "single-value-0123456789" });
        Exception ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Redaction:ApiKeys must be a list", ex.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("single-value", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Window_over_the_timer_ceiling_fails_host_startup_rather_than_the_first_request()
    {
        using ConfiguredFixture factory = new(new Dictionary<string, string?> { ["Redaction:RateLimit:WindowSeconds"] = "5000000" });
        Exception ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Redaction:RateLimit:WindowSeconds must be at most", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void License_and_keys_bind_from_configuration()
    {
        Dictionary<string, string?> settings = Keyed();
        settings["Redaction:QuestPdfLicense"] = "Professional";
        using ConfiguredFixture factory = new(settings);

        RedactionSettings bound = factory.Services.GetRequiredService<IOptions<RedactionSettings>>().Value;
        Assert.Equal(QuestPdfLicense.Professional, bound.QuestPdfLicense);
        Assert.Equal([KeyA, KeyB], bound.ApiKeys);
        Assert.False(factory.Services.GetRequiredService<ApiKeyAuthenticator>().IsOpen);
    }
}
