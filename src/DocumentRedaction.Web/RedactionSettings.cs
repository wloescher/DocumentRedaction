using DocumentRedaction.Documents;
using Microsoft.Extensions.Options;

namespace DocumentRedaction.Web;

/// <summary>Bound from the "Redaction" configuration section and validated at startup.</summary>
public sealed class RedactionSettings
{
    public const string SectionName = "Redaction";

    /// <summary>
    /// Uploads are buffered into one array, so the limit must leave room for that array and for
    /// the request-body slack on top of it.
    /// </summary>
    public const long MaxUploadBytesCeiling = int.MaxValue - RequestBodySlackBytes;

    /// <summary>Headroom over <see cref="MaxUploadBytes"/> for multipart boundaries and the other form fields.</summary>
    private const long RequestBodySlackBytes = 64 * 1024;

    /// <summary>Largest upload accepted, in bytes. Enforced by the API, the page, Kestrel and the form reader.</summary>
    public long MaxUploadBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>Parsing caps for Word parts and PDF streams, pages and text; see <see cref="DocumentLimits"/>.</summary>
    public DocumentLimits Limits { get; set; } = new();

    /// <summary>
    /// Keys accepted in the <c>X-Api-Key</c> header on <c>/api</c>. Empty (the default) leaves the
    /// API open, which is logged as a warning at startup. The Blazor page calls the service
    /// in-process and never needs a key.
    /// </summary>
    public IList<string> ApiKeys { get; } = [];

    /// <summary>Fixed-window throttle on <c>/api</c>, per API key or, without a valid key, per client address.</summary>
    public RateLimitSettings RateLimit { get; set; } = new();

    /// <summary>The QuestPDF tier this deployment is entitled to; see the README before deploying commercially.</summary>
    public QuestPdfLicense QuestPdfLicense { get; set; } = QuestPdfLicense.Community;

    /// <summary>The request-body limit Kestrel and the form reader apply so a maximum-size file still fits.</summary>
    public long RequestBodyLimit => MaxUploadBytes + RequestBodySlackBytes;
}

/// <summary>Bound from <c>Redaction:RateLimit</c>.</summary>
public sealed class RateLimitSettings
{
    /// <summary>Set false when a gateway in front of the host throttles instead.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Requests allowed per partition in each window.</summary>
    public int PermitLimit { get; set; } = 60;

    /// <summary>Window length; a partition's count resets when it elapses. At most <see cref="MaxWindowSeconds"/>.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>The limiter replenishes on a <see cref="System.Threading.Timer"/>, whose due time is capped at 4,294,967,294 ms.</summary>
    public const int MaxWindowSeconds = 4_294_967;

    public TimeSpan Window => TimeSpan.FromSeconds(WindowSeconds);
}

/// <summary>Fails host startup (via <c>ValidateOnStart</c>) rather than the first upload when the section is misconfigured.</summary>
public sealed class RedactionSettingsValidator : IValidateOptions<RedactionSettings>
{
    /// <summary>Shorter keys are guessable.</summary>
    public const int MinimumApiKeyLength = 16;

    public ValidateOptionsResult Validate(string? name, RedactionSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string uploadKey = $"{RedactionSettings.SectionName}:{nameof(RedactionSettings.MaxUploadBytes)}";
        if (DocumentLimits.AtLeastOne(options.MaxUploadBytes, uploadKey) is { } tooSmall)
        {
            return ValidateOptionsResult.Fail(tooSmall);
        }

        if (options.MaxUploadBytes > RedactionSettings.MaxUploadBytesCeiling)
        {
            return ValidateOptionsResult.Fail(
                $"{uploadKey} must be at most {RedactionSettings.MaxUploadBytesCeiling:N0} but is {options.MaxUploadBytes:N0}.");
        }

        if (!options.Limits.TryValidate(out string? error))
        {
            return ValidateOptionsResult.Fail($"{RedactionSettings.SectionName}:{nameof(RedactionSettings.Limits)}:{error}");
        }

        if (!Enum.IsDefined(options.QuestPdfLicense))
        {
            // The binder accepts any integer for an enum; catch it here rather than when the PDF processor is built.
            return ValidateOptionsResult.Fail(
                $"{RedactionSettings.SectionName}:{nameof(RedactionSettings.QuestPdfLicense)} must be one of {string.Join(", ", Enum.GetNames<QuestPdfLicense>())}.");
        }

        return ValidateApiKeys(options.ApiKeys) ?? ValidateRateLimit(options.RateLimit) ?? ValidateOptionsResult.Success;
    }

    /// <summary>
    /// Keys are visible ASCII only: header values are sent and decoded as bytes, so anything else
    /// depends on the client's encoding and fails as a mystery 401. Messages never echo the key.
    /// </summary>
    private static ValidateOptionsResult? ValidateApiKeys(IList<string> keys)
    {
        string keysKey = $"{RedactionSettings.SectionName}:{nameof(RedactionSettings.ApiKeys)}";
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            if (string.IsNullOrEmpty(key) || key.Length < MinimumApiKeyLength)
            {
                return ValidateOptionsResult.Fail($"{keysKey}:{i} must be at least {MinimumApiKeyLength} characters.");
            }

            if (key.Any(c => c is < '!' or > '~'))
            {
                return ValidateOptionsResult.Fail($"{keysKey}:{i} must contain only visible ASCII characters (no spaces).");
            }

            if (keys.Take(i).Contains(key, StringComparer.Ordinal))
            {
                // A repeated key would be authenticated as its first occurrence and never own its own rate-limit window.
                return ValidateOptionsResult.Fail($"{keysKey}:{i} repeats an earlier key.");
            }
        }

        return null;
    }

    private static ValidateOptionsResult? ValidateRateLimit(RateLimitSettings rateLimit)
    {
        string prefix = $"{RedactionSettings.SectionName}:{nameof(RedactionSettings.RateLimit)}";
        if (DocumentLimits.AtLeastOne(rateLimit.PermitLimit, $"{prefix}:{nameof(RateLimitSettings.PermitLimit)}") is { } permit)
        {
            return ValidateOptionsResult.Fail(permit);
        }

        string windowKey = $"{prefix}:{nameof(RateLimitSettings.WindowSeconds)}";
        if (DocumentLimits.AtLeastOne(rateLimit.WindowSeconds, windowKey) is { } window)
        {
            return ValidateOptionsResult.Fail(window);
        }

        if (rateLimit.WindowSeconds > RateLimitSettings.MaxWindowSeconds)
        {
            return ValidateOptionsResult.Fail($"{windowKey} must be at most {RateLimitSettings.MaxWindowSeconds:N0} but is {rateLimit.WindowSeconds:N0}.");
        }

        return null;
    }
}
