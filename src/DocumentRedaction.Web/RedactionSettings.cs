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

    /// <summary>The request-body limit Kestrel and the form reader apply so a maximum-size file still fits.</summary>
    public long RequestBodyLimit => MaxUploadBytes + RequestBodySlackBytes;
}

/// <summary>Fails host startup (via <c>ValidateOnStart</c>) rather than the first upload when the section is misconfigured.</summary>
public sealed class RedactionSettingsValidator : IValidateOptions<RedactionSettings>
{
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

        return options.Limits.TryValidate(out string? error)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{RedactionSettings.SectionName}:{nameof(RedactionSettings.Limits)}:{error}");
    }
}
