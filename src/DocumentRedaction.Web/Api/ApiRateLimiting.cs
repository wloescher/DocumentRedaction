using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DocumentRedaction.Web.Api;

/// <summary>
/// Fixed-window throttle for the <c>/api</c> group. Each valid API key gets its own window; every
/// other caller (open mode, or a missing or wrong key) shares a window per client address, so
/// guessing keys is throttled too. Rejections are 429 problem responses with <c>Retry-After</c>.
/// </summary>
public static class ApiRateLimiting
{
    public const string PolicyName = "api";

    /// <summary>Partition name used when the limiter is switched off; the no-op limiter never rejects.</summary>
    private const string DisabledPartition = "disabled";

    /// <summary>Configured through options so the settings are bound after the host is built, the same way Kestrel's limits are.</summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // AddRateLimiter registers the marker UseRateLimiter checks for; the policy itself needs the bound settings.
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RedactionSettings>, ApiKeyAuthenticator>((options, settings, authenticator) =>
            {
                RateLimitSettings rateLimit = settings.Value.RateLimit;
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, _) => OnRejected(context.HttpContext, rateLimit);
                options.AddPolicy(PolicyName, httpContext => Partition(httpContext, rateLimit, authenticator));
            });
        return services;
    }

    private static RateLimitPartition<string> Partition(HttpContext httpContext, RateLimitSettings settings, ApiKeyAuthenticator authenticator)
    {
        if (!settings.Enabled)
        {
            return RateLimitPartition.GetNoLimiter(DisabledPartition);
        }

        return RateLimitPartition.GetFixedWindowLimiter(PartitionKey(httpContext, authenticator), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitLimit,
            Window = settings.Window,
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }

    /// <summary>The key index rather than the key, so no secret is held as a partition name.</summary>
    internal static string PartitionKey(HttpContext httpContext, ApiKeyAuthenticator authenticator)
    {
        if (authenticator.TryAuthenticate(httpContext, out int keyIndex) && keyIndex >= 0)
        {
            return "key:" + keyIndex.ToString(CultureInfo.InvariantCulture);
        }

        return "address:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }

    /// <summary>
    /// The fixed-window limiter reports a whole window as its retry-after, never the time left in
    /// the current one, so the header is the window length: an upper bound, as RFC 9110 allows.
    /// </summary>
    private static ValueTask OnRejected(HttpContext httpContext, RateLimitSettings settings)
    {
        httpContext.Response.Headers.RetryAfter = settings.WindowSeconds.ToString(CultureInfo.InvariantCulture);
        return new ValueTask(ApiProblems.TooManyRequests(settings).ExecuteAsync(httpContext));
    }
}
