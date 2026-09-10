namespace DocumentRedaction.Web;

/// <summary>Bound from the "Redaction" configuration section.</summary>
public sealed class RedactionSettings
{
    public const string SectionName = "Redaction";

    /// <summary>Largest upload accepted, in bytes. Enforced by the API, the page and Kestrel.</summary>
    public long MaxUploadBytes { get; set; } = 25 * 1024 * 1024;
}
