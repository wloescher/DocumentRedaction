namespace DocumentRedaction.Core.Model;

/// <summary>Static metadata describing an <see cref="InformationKind"/>.</summary>
/// <param name="Kind">The kind being described.</param>
/// <param name="DisplayName">Human-readable name for UIs and reports.</param>
/// <param name="PlaceholderLabel">Label substituted into <see cref="RedactionOptions.PlaceholderFormat"/>.</param>
/// <param name="Categories">Every category that includes this kind.</param>
/// <param name="Priority">
/// Tie-breaker when two detections of equal length overlap. Lower values win; kinds whose
/// detectors validate a checksum are ranked ahead of purely pattern-based kinds.
/// </param>
/// <param name="WidensToSentence">
/// True when the detector reports the whole sentence around a marker rather than a token, so a
/// line break bounds the detection. Processors that join lines to catch wrapped tokens must
/// leave these kinds out of that pass.
/// </param>
/// <param name="MetadataOnly">
/// True for kinds that live in document metadata (an author field) rather than in text. They
/// have no text detector; document processors redact them by structure and report them by kind.
/// </param>
public sealed record InformationKindInfo(
    InformationKind Kind,
    string DisplayName,
    string PlaceholderLabel,
    RedactionCategory Categories,
    int Priority,
    bool WidensToSentence = false,
    bool MetadataOnly = false);
