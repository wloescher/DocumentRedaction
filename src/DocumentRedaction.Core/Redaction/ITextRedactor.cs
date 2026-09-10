using DocumentRedaction.Core.Model;

namespace DocumentRedaction.Core.Redaction;

/// <summary>Detects and replaces sensitive spans in plain text.</summary>
public interface ITextRedactor
{
    /// <summary>Non-overlapping detections for the enabled kinds, ordered by position.</summary>
    IReadOnlyList<Detection> Detect(string text, RedactionOptions options);

    /// <summary>Detects and substitutes placeholders in one step.</summary>
    TextRedactionResult Redact(string text, RedactionOptions options);

    /// <summary>
    /// Substitutes placeholders for detections previously produced by <see cref="Detect"/> on
    /// the same text. Lets document processors detect once and apply per fragment.
    /// </summary>
    string Apply(string text, IReadOnlyList<Detection> detections, RedactionOptions options);
}
